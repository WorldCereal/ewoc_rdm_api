using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IIASA.WorldCereal.Rdm.Core;
using IIASA.WorldCereal.Rdm.Entity;
using IIASA.WorldCereal.Rdm.Enums;
using IIASA.WorldCereal.Rdm.Jobs.UploadUserDataset;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.IO;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace IIASA.WorldCereal.Rdm.Jobs.Validation
{
    public class UserDataSetValidationJob : AsyncBackgroundJob<ValidationArgs>, ITransientDependency, IUnitOfWorkEnabled
    {
        private readonly IRepository<ValidationRule> _validationRulesRepository;
        private readonly ILogger<UserDataSetValidationJob> _logger;
        private readonly IBackgroundJobManager _backgroundJobManager;
        private readonly IRepository<UserDataset> _userDatasetRepository;

        public UserDataSetValidationJob(IRepository<ValidationRule> validationRulesRepository,
            ILogger<UserDataSetValidationJob> logger,
            IBackgroundJobManager backgroundJobManager,
            IRepository<UserDataset> userDatasetRepository)
        {
            _validationRulesRepository = validationRulesRepository;
            _logger = logger;
            _backgroundJobManager = backgroundJobManager;
            _userDatasetRepository = userDatasetRepository;
        }

        public override async Task ExecuteAsync(ValidationArgs args)
        {
            var userDataset = await _userDatasetRepository.GetAsync(x => x.Id == args.UserDatasetId);
            VectorFileReader reader = null;

            _logger.LogInformation($"Validating user dataset-{userDataset.CollectionId}.File- {args.Path}");
            try
            {
                reader = VectorFileReader.GetReader(args.Path);
                await Validate(userDataset, reader);
            }
            catch (Exception)
            {
                userDataset.State = UserDatasetState.ValidationFailedUploadRequired;
                userDataset.Errors = new[] {"Internal Server Error."};
            }       
            finally
            {
                reader?.Dispose();
            }

            // start dataset upload job;
            await StartNextJob(args, userDataset);

            await _userDatasetRepository.UpdateAsync(userDataset);
        }

        private async Task Validate(UserDataset userDataset, VectorFileReader reader)
        {
            var typeShape = reader.GetShapeGeometryType();
            var rules = await _validationRulesRepository.Include(x => x.RuleValidValues)
                .AsNoTracking().ToListAsync();
            var mandatoryFieldsRules = GetMandatoryFields(typeShape, rules);

            var allFieldNames = reader.GetAllFieldName();

            // check for missing fields
            var errorsForMissingFields = GetErrorsForMissingFields(mandatoryFieldsRules, allFieldNames);

            //For valid available attributes, check the attribute values
            var rulesForAvailableFields = rules.Where(x => allFieldNames.Contains(x.AttributeName));
            List<AttributeValidationDto> availableAttributeList =
                GetValidatorsForAvailableAttributes(reader, rulesForAvailableFields);

            while (reader.Read())
            {
                foreach (var availableField in availableAttributeList)
                {
                    var val = reader.GetValue(availableField.Index);

                    if (availableField.FieldValidator.IsValid(val))
                    {
                        continue;
                    }

                    var errorString = val?.ToString();
                    if (availableField.ErrorValues.Contains(errorString) == false)
                    {
                        availableField.ErrorValues.Add(errorString);
                    }
                }
            }

            UpdateJobStatus(errorsForMissingFields, availableAttributeList, userDataset);
            _logger.LogInformation($"Validation completed. user dataset-{userDataset.CollectionId}.");
        }

        private List<AttributeValidationDto> GetValidatorsForAvailableAttributes(VectorFileReader reader,
            IEnumerable<ValidationRule> rulesForAvailableFields)
        {
            var availableAttributeList = new List<AttributeValidationDto>();
            foreach (var availableField in rulesForAvailableFields)
            {
                var item = new AttributeValidationDto
                {
                    Index = reader.GetOrdinalExt(availableField.AttributeName),
                    ValidationRule = availableField,
                    FieldValidator = GetFieldValidator(availableField)
                };

                availableAttributeList.Add(item);
            }

            return availableAttributeList;
        }

        private async Task StartNextJob(ValidationArgs args, UserDataset userDataset)
        {
            if (userDataset.State == UserDatasetState.ValidationSuccessfulProvisionInProgressWait)
            {
                await _backgroundJobManager.EnqueueAsync(new ProvisionUserDataSetJobArgs
                    {
                        DirectoryPathToClean = args.DirectoryPathToClean,
                        Path = args.Path,
                        UserDatasetId = args.UserDatasetId,
                        ZipFilePath =  args.ZipFilePath
                    },
                    delay: TimeSpan.FromMilliseconds(10));
            }
            else
            {
                Directory.Delete(args.DirectoryPathToClean, true);
            }
        }

        private void UpdateJobStatus(List<string> errorsForMissingFields, List<AttributeValidationDto> availableList,
            UserDataset userDataset)
        {
            if (errorsForMissingFields.Any() || availableList.SelectMany(x => x.ErrorValues).Any())
            {
                // shape file has errors
                userDataset.State = UserDatasetState.ValidationFailedUploadRequired;
                userDataset.Errors = GetErrors(errorsForMissingFields, availableList);
                _logger.LogInformation($"Validation found errors. user dataset-{userDataset.CollectionId}.");
            }
            else
            {
                // all checks passed, user dataset has no errors.
                userDataset.State = UserDatasetState.ValidationSuccessfulProvisionInProgressWait;
                userDataset.Errors = Array.Empty<string>();
            }
        }

        private IFieldValidator GetFieldValidator(ValidationRule availableField)
        {
            var fieldValidator = GetAllValidators().First(x => availableField.ValueChecks.Contains(x.ValueCheckType));

            fieldValidator.AttributeName = availableField.AttributeName;
            fieldValidator.SetValidValues(availableField.RuleValidValues.Select(x => x.Value).ToArray());

            return fieldValidator;
        }

        private static List<IFieldValidator> GetAllValidators()
        {
            return new()
            {
                new IntegerFieldValidator(), new DecimalFieldValidator(), new DateFieldValidator(),
                new TextFiedlValidator()
            };
        }

        private static List<string> GetErrorsForMissingFields(IEnumerable<ValidationRule> mandatoryFields,
            string[] allFieldNames)
        {
            var errors = new List<string>();
            foreach (var mandatoryField in mandatoryFields)
            {
                if (allFieldNames.Any(x => x == mandatoryField.AttributeName))
                {
                    continue;
                }

                errors.Add(mandatoryField.MissingErrorMessage);
            }

            return errors;
        }

        private static IEnumerable<ValidationRule> GetMandatoryFields(ShapeGeometryType typeShape,
            List<ValidationRule> rules)
        {
            IEnumerable<ValidationRule> mandatoryFields = new List<ValidationRule>();

            if (typeShape == ShapeGeometryType.Polygon)
            {
                mandatoryFields = rules.Where(x => x.MandatoryType == MandatoryType.PointAndPolygon
                                                   || x.MandatoryType == MandatoryType.OnlyForPolygon);
            }

            if (typeShape == ShapeGeometryType.Point)
            {
                mandatoryFields = rules.Where(x => x.MandatoryType == MandatoryType.PointAndPolygon
                                                   || x.MandatoryType == MandatoryType.OnlyForPoint);
            }

            return mandatoryFields;
        }

        private string[] GetErrors(List<string> errors, List<AttributeValidationDto> availableList)
        {
            var errorResults = new List<string>();
            errorResults.AddRange(errors);

            foreach (var field in availableList)
            {
                if (field.ErrorValues.Any() == false)
                {
                    continue;
                }

                errorResults.Add(field.ValidationRule.InvalidValueErrorMessage +
                                 $":{string.Join(",", field.ErrorValues)}");
            }

            return errorResults.ToArray();
        }
    }
}
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IIASA.WorldCereal.Rdm.Entity;
using IIASA.WorldCereal.Rdm.Enums;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace IIASA.WorldCereal.Rdm.Core;

public interface IAccessProvider
{
    Task<bool> IsAdmin();

    bool IsLoggedIn();
    //Task<AccessType[]> GetAllowedAccess();

    Task<UserDatasetState[]> GetCommunityAllowedUserDatasetStates();
    Task<AccessType[]> GetConsortiumAllowedAccess();
    Task<AccessType[]> GetCommunityAllowedAccess();

    Task<ExternalUserAccount> User();

    Task<Guid> UserId();
}

public class AccessProvider : IAccessProvider
{
    private readonly ICurrentUser _loggedInUser;
    private readonly IRepository<ExternalUserAccount, int> _accountRepository;
    private ExternalUserAccount? _user;


    public AccessProvider(ICurrentUser loggedInUser, IRepository<ExternalUserAccount, int> accountRepository)
    {
        _loggedInUser = loggedInUser;
        _accountRepository = accountRepository;
    }

    public async Task<ExternalUserAccount> User()
    {
        if (_user != null)
        {
            return _user;
        }

        var email = _loggedInUser.Email?.ToLowerInvariant();
        _user = await _accountRepository.FirstOrDefaultAsync(x => x.EmailId == email);
        if (_user == null)
        {
            _user = await _accountRepository.InsertAsync(new ExternalUserAccount
            {
                UserId = _loggedInUser.Id.Value,
                UserName = $"{_loggedInUser.Name} {_loggedInUser.SurName}",
                EmailId = email
            });
        }
        else if (_user.UserId != _loggedInUser.Id.Value)
        {
            _user.UserId = _loggedInUser.Id.Value;
            _user.UserName = $"{_loggedInUser.Name} {_loggedInUser.SurName}";
            await _accountRepository.UpdateAsync(_user);
        }

        return _user;
    }

    public async Task<bool> IsAdmin()
    {
        if (IsLoggedIn() == false)
        {
            return false;
        }

        return (await User()).UserRole == UserRole.Admin;
    }

    public async Task<bool> IsModerator()
    {
        if (IsLoggedIn() == false)
        {
            return false;
        }

        return (await User()).UserRole == UserRole.Moderator;
    }

    public async Task<bool> IsElevatedUser()
    {
        if (IsLoggedIn() == false)
        {
            return false;
        }

        return (await User()).UserRole == UserRole.ElevatedUser;
    }

    public bool IsLoggedIn()
    {
        if (_loggedInUser == null || _loggedInUser.Id == null)
        {
            return false;
        }

        return true;
    }

    public async Task<Guid> UserId()
    {
        return (await User()).UserId;
    }

    /*public async Task<AccessType[]> GetAllowedAccess()
    {
        var allowedAccess = new[] { AccessType.Public };
        if (IsLoggedIn() == false)
        {
            return allowedAccess;
        }

        try
        {
            if (await IsAdmin())
            {
                return
                [
                    AccessType.Public, AccessType.Private, AccessType.PrivateShared, AccessType.PrivateSharedVerified
                ];
            }

            if (await IsModerator())
            {
                return
                [
                    AccessType.Public, AccessType.PrivateShared, AccessType.PrivateSharedVerified
                ];
            }

            if (await IsElevatedUser())
            {
                return
                [
                    AccessType.Public, AccessType.PrivateSharedVerified
                ];
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return allowedAccess;
    }*/

    public async Task<UserDatasetState[]> GetCommunityAllowedUserDatasetStates()
    {
        if (await IsAdmin())
        {
            // all access
            return (UserDatasetState[])Enum.GetValues(typeof(UserDatasetState));
        }

        if (await IsModerator())
        {
            // individual user private datasets not allowed. All user datasets in "Available in Module" state not allowed.
            return
            [
                UserDatasetState.PrivateSharedWithConsortium, UserDatasetState.PrivateSharedWithConsortiumVerified
            ];
        }

        if (await IsElevatedUser())
        {
            // only verified shared user datasets are not allowed.
            return
            [
                UserDatasetState.PrivateSharedWithConsortiumVerified
            ];
        }

        return [];
    }

    public async Task<AccessType[]> GetCommunityAllowedAccess()
    {
        var allowedAccess = new[] { AccessType.Public };
        if (IsLoggedIn() == false)
        {
            return allowedAccess;
        }

        try
        {
            if (await IsAdmin())
            {
                return
                [
                    AccessType.Public, AccessType.Private, AccessType.PrivateShared, AccessType.PrivateSharedVerified
                ];
            }

            if (await IsModerator())
            {
                // individual user private datasets not allowed. All user datasets in "Available in Module" state not allowed.
                return
                [
                    AccessType.Public, AccessType.PrivateShared, AccessType.PrivateSharedVerified
                ];
            }

            if (await IsElevatedUser())
            {
                // user private and unverified shared datasets are not allowed.
                return
                [
                    AccessType.Public, AccessType.PrivateSharedVerified
                ];
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return allowedAccess;
    }

    public async Task<AccessType[]> GetConsortiumAllowedAccess()
    {
        var allowedAccess = new[] { AccessType.Public };
        if (IsLoggedIn() == false)
        {
            return allowedAccess;
        }

        try
        {
            if (await IsAdmin() || await IsModerator() || await IsElevatedUser())
            {
                // there is no AccessType.PrivateShared, AccessType.PrivateSharedVerified for consortium datasets.
                return
                [
                    AccessType.Public, AccessType.Private
                ];
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return allowedAccess;
    }
}
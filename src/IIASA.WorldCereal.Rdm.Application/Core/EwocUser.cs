using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IIASA.WorldCereal.Rdm.ServiceConfigs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace IIASA.WorldCereal.Rdm.Core
{
    // TODO when we get clarity on key cloak/Kong config.

    public class EwocUser : IEwocUser
    {
        private readonly ILogger<IEwocUser> _logger;
        private readonly EwocConfig _ewocConfig;
        private readonly IHeaderDictionary _headers;
        private UserInfo? _userInfo;

        public EwocUser(IHttpContextAccessor httpContextAccessor, ILogger<EwocUser> logger, EwocConfig ewocConfig)
        {
            _logger = logger;
            _ewocConfig = ewocConfig;
            _headers = httpContextAccessor.HttpContext?.Request.Headers;
        }

        public UserInfo UserInfo => _userInfo ??= GetUserInfo();

        public string UserId
        {
            get
            {
                _logger.LogDebug("Accessing UserId in the request");
                return UserInfo == null ? string.Empty : UserInfo.UserId;
            }
        }

        public string UserName
        {
            get
            {
                _logger.LogDebug("Accessing UserName in the request");
                return UserInfo == null ? string.Empty : UserInfo.Username;
            }
        }

        public string Group
        {
            get
            {
                _logger.LogDebug("Accessing Group in the request");
                return UserInfo == null ? string.Empty : UserInfo.Group.FirstOrDefault();
            }
        }

        public bool IsAdmin
        {
            get
            {
                if (_ewocConfig.AuthEnabled == false)
                {
                    return true; // remove later if required.
                }

                return string.IsNullOrEmpty(UserId) == false && string.IsNullOrEmpty(Group) == false
                                                             && _ewocConfig.AdminGroupNames.Contains(Group);
            }
        }

        public bool IsAuthenticated => string.IsNullOrEmpty(UserId) == false && string.IsNullOrEmpty(Group) == false;

        public bool CanAccessUserData => IsAdmin || IsAuthenticated;

        private UserInfo? GetUserInfo()
        {
            var userInfo = _headers[_ewocConfig.UserInfo];
            if (userInfo.Count == 0)
            {
                return null;
            }
            
            var encodedTextBytes = Convert.FromBase64String(userInfo);
            var decodedString = Encoding.UTF8.GetString(encodedTextBytes);
            var userInfoObj = JsonConvert.DeserializeObject<UserInfo>(decodedString);
            return userInfoObj;
        }
    }

    public class UserInfo
    {
        [JsonProperty("email_verified")] public bool EmailVerified { get; set; }

        [JsonProperty("preferred_username")] public string PreferredUsername { get; set; }

        [JsonProperty("userid")] public string UserId { get; set; }

        [JsonProperty("sub")] public string Sub { get; set; }

        [JsonProperty("id")] public string Id { get; set; }

        [JsonProperty("username")] public string Username { get; set; }

        [JsonProperty("group")] public List<string> Group { get; set; }

        [JsonProperty("attribute")] public string Attribute { get; set; }
    }
}
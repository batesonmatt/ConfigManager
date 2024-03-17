using System;
using System.DirectoryServices.AccountManagement;
using System.Linq;

namespace ConfigManager.Security
{
    public class ActiveDirectoryUser
    {
        #region Properties

        public string Name => _name;
        public string Domain => _domain;
        public string Host => _host;

        #endregion

        #region Fields

        private readonly string _name = GetName();
        private readonly string _domain = GetDomain();
        private readonly string _host = GetHost();
        private readonly string _adminGroup = "Administrators";

        #endregion

        #region Methods

        private static string GetName()
        {
            string username;

            try
            {
                username = Environment.UserName ?? string.Empty;
            }
            catch
            {
                username = string.Empty;
            }

            return username;
        }

        private static string GetDomain()
        {
            string domain;

            try
            {
                domain = Environment.UserDomainName ?? string.Empty;
            }
            catch
            {
                domain = string.Empty;
            }

            return domain;
        }

        private static string GetHost()
        {
            string host;

            try
            {
                host = Environment.MachineName ?? string.Empty;
            }
            catch
            {
                host = string.Empty;
            }

            return host;
        }

        public string GetFullDomainUserName()
        {
            string name;
            string[] login;

            try
            {
                login = new string[] { _domain, _name };

                name = string.Join(@"\", login.Where(s => !string.IsNullOrWhiteSpace(s)));
            }
            catch
            {
                name = string.Empty;
            }

            return name;
        }

        public bool IsAdmin()
        {
            bool result = false;
            PrincipalContext domain;
            UserPrincipal user;
            GroupPrincipal group;

            try
            {
                // System.Security.Principal.WindowsIdentity.GetCurrent().Name may also be used here.

                domain = new(ContextType.Domain, _domain);
                user = UserPrincipal.FindByIdentity(domain, _name);
                group = GroupPrincipal.FindByIdentity(domain, _adminGroup);

                if (user is not null)
                {
                    if (group is not null)
                    {
                        result = group.GetMembers(recursive: true).Contains(user);
                    }
                }
            }
            catch
            {
                result = false;
            }

            return result;
        }

        #endregion
    }
}

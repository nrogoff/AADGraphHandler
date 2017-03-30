// Copyright (c) 2017 Hard Medium Soft Ltd.  
// 
// Author: Nicholas Rogoff
// Created: 2017 - 02 - 27
// 
// Project: hms.Common.Azure.AADGraphHandler
// Filename: AADGraphHandler.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.ActiveDirectory.GraphClient;
using Microsoft.Azure.ActiveDirectory.GraphClient.Extensions;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace hms.Common.Azure.AADGraphHandler
{
    /// <summary>
    /// Azure Active Directory Client and Graph Handler
    /// </summary>
    public class AADGraphHandler : IAADGraphHandler
    {
        public string CachePrefix { get; set; } = "HMSAAD";

        private readonly AADGraphHandlerConfigurationForApp _appConfig;
        private readonly AADGraphHandlerConfigurationForUser _userConfig;
        private ActiveDirectoryClient _aadClient;
        private readonly ICacheProvider _cacheProvider;

        /// <summary>
        /// The Active Directory Client created. This can be used to directly access operations not covered by the Handler.
        /// </summary>
        public ActiveDirectoryClient AADClient => _aadClient;

        public string TokenForApplication { get; set; }
        public string TokenForUser { get; set; }

        #region Constructors

        /// <summary>
        /// Constructor for Application context client
        /// </summary>
        /// <param name="appConfig">The Application Config object. This needs to be populated prior to construction</param>
        public AADGraphHandler(AADGraphHandlerConfigurationForApp appConfig)
        {
            _appConfig = appConfig;
            _aadClient = GetAADClientAsApp();
            _cacheProvider = appConfig.CacheProvider;
        }

        /// <summary>
        /// Constructor for User context client
        /// </summary>
        /// <param name="userConfig">The User Config object</param>
        public AADGraphHandler(AADGraphHandlerConfigurationForUser userConfig)
        {
            _userConfig = userConfig;
            _aadClient = GetAADClientAsUser();
            _cacheProvider = userConfig.CacheProvider;
        }

        #endregion

        #region AADClient Setup

        /// <summary>
        /// Creates an AAD Client
        /// </summary>
        /// <returns>An Active Directory Client</returns>
        private ActiveDirectoryClient GetAADClientAsApp()
        {
            ActiveDirectoryClient activeDirectoryClient = new ActiveDirectoryClient(_appConfig.TenantGraphServiceRootUri,
                async () => await GetTokenForApplicationAsync(_appConfig.TenantAuthorityServiceUri.AbsoluteUri, _appConfig.AppClientId, _appConfig.AppClientSecret));
            return activeDirectoryClient;
        }

        /// <summary>
        /// Get Token for Application.
        /// </summary>
        /// <param name="authority">"https://login.microsoftonline.com/{tenantId}"</param>
        /// <param name="aadClientId">The Application Client Id in AAD</param>
        /// <param name="aadClientSecret">The Application client secret in AAD</param>
        /// <returns>Token for application.</returns>
        private async Task<string> GetTokenForApplicationAsync(string authority, string aadClientId, string aadClientSecret)
        {
            if (TokenForApplication == null)
            {
                AuthenticationContext authenticationContext = new AuthenticationContext(authority, false);
                // Config for OAuth client credentials 
                ClientCredential clientCred = new ClientCredential(aadClientId, aadClientSecret);
                AuthenticationResult authenticationResult =
                    await authenticationContext.AcquireTokenAsync(_appConfig.GraphServiceRootUri.AbsoluteUri,
                        clientCred);
                TokenForApplication = authenticationResult.AccessToken;
            }
            return TokenForApplication;
        }

        /// <summary>
        /// Get Active Directory Client for User.
        /// </summary>
        /// <returns>ActiveDirectoryClient for User.</returns>
        private ActiveDirectoryClient GetAADClientAsUser()
        {
            Uri serviceRoot = new Uri(_userConfig.TenantGraphServiceRootUri, _userConfig.TenantId);
            ActiveDirectoryClient activeDirectoryClient = new ActiveDirectoryClient(serviceRoot,
                async () => await GetTokenForUserAsync());
            return activeDirectoryClient;
        }

        private async Task<string> GetTokenForUserAsync()
        {
            if (TokenForUser == null)
            {

                AuthenticationContext authenticationContext = new AuthenticationContext(_userConfig.TenantAuthorityServiceUri.AbsoluteUri, false);
                AuthenticationResult userAuthnResult;
                if (!string.IsNullOrEmpty(_userConfig.Username))
                {
                    userAuthnResult = await authenticationContext.AcquireTokenAsync(
                        _userConfig.GraphServiceRootUri.AbsoluteUri,
                        _userConfig.AppClientId,
                        _userConfig.RedirectUri,
                        new PlatformParameters(PromptBehavior.RefreshSession),
                        new UserIdentifier(_userConfig.Username, UserIdentifierType.RequiredDisplayableId));
                }
                else
                {
                    userAuthnResult = await authenticationContext.AcquireTokenAsync(
                        _userConfig.GraphServiceRootUri.AbsoluteUri,
                        _userConfig.AppClientId,
                        _userConfig.RedirectUri,
                        new PlatformParameters(PromptBehavior.RefreshSession));
                }


                TokenForUser = userAuthnResult.AccessToken;
                Console.WriteLine("\n Welcome " + userAuthnResult.UserInfo.GivenName + " " +
                                  userAuthnResult.UserInfo.FamilyName);
            }
            return TokenForUser;
        }

        #endregion

        #region Tenant operations

        /// <summary>
        /// Get the Tenant Details
        /// </summary>
        /// <returns></returns>
        /// <remarks>The following section may be run by any user, as long as the app
        /// has been granted the minimum of User.Read (and User.ReadWrite to update photo)
        /// and User.ReadBasic.All scope permissions. Directory.ReadWrite.All
        /// or Directory.AccessAsUser.All will also work, but are much more privileged.
        /// </remarks>
        public async Task<ITenantDetail> GetTenantDetailsAsync()
        {
            IPagedCollection<ITenantDetail> tenantsCollection = await AADClient.TenantDetails.ExecuteAsync();
            ITenantDetail tenantDetail = tenantsCollection.CurrentPage.ToList().FirstOrDefault();
            return tenantDetail;
        }

        /// <summary>
        /// get the default domain name. This can be the same as the initial domain, but may also be a  custom domain.
        /// </summary>
        /// <returns></returns>
        /// <remarks>see remarks for GetTenantDetailsAsync</remarks>
        public string GetTenantDefaultDomain()
        {
            var tenantDetails = GetTenantDetailsAsync().Result;
            var defaultDomain = tenantDetails.VerifiedDomains.First(x => x.@default.HasValue && x.@default.Value);
            return defaultDomain.Name;
        }

        /// <summary>
        /// Get the initial domain name. Usually the .onMicrosoft.com domain.
        /// </summary>
        /// <returns></returns>
        /// <remarks>see remarks for GetTenantDetailsAsync</remarks>
        public string GetTenantInitialDomain()
        {
            var tenantDetails = GetTenantDetailsAsync().Result;
            var initialDomain = tenantDetails.VerifiedDomains.First(x => x.Initial.HasValue && x.Initial.Value);
            return initialDomain.Name;
        }

        #endregion

        #region User Operations

        /// <summary>
        /// Get the currently signed in User
        /// </summary>
        /// <returns></returns>
        public async Task<User> GetSignedInUserAsync()
        {
            if (_userConfig == null)
            {
                throw new ArgumentException("The Delete User operation requires the Active Directory Client to be as a User context. Please use the User config constructor.");
            }

            var signedInUser = (User)await AADClient.Me.ExecuteAsync();

            return signedInUser;
        }

        /// <summary>
        /// Get the currently signed in User
        /// </summary>
        /// <returns></returns>
        public User GetSignedInUser()
        {
            return GetSignedInUserAsync().Result;

        }

        /// <summary>
        /// Creates a new user account
        /// </summary>
        /// <param name="newUser"></param>
        /// <returns></returns>
        /// <remarks>Requires Directory.ReadWrite.All or Directory.AccessAsUser.All, and the signed in user
        /// must be a privileged user (like a company or user admin)</remarks>
        public async Task<bool> CreateNewUserAsync(User newUser)
        {
            try
            {
                await _aadClient.Users.AddUserAsync(newUser);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
        }

        /// <summary>
        /// Creates a new user account
        /// </summary>
        /// <param name="newUser"></param>
        /// <returns></returns>
        /// <remarks>Requires Directory.ReadWrite.All or Directory.AccessAsUser.All, and the signed in user
        /// must be a privileged user (like a company or user admin)</remarks>
        public bool CreateNewUser(User newUser)
        {
            return CreateNewUserAsync(newUser).Result;
        }

        /// <summary>
        /// Update a user
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        /// <remarks>Will update the cache provider if implemented</remarks>
        public async Task UpdateUserAsync(IUser user)
        {
            await user.UpdateAsync();

            string cacheKey = $"{CachePrefix}:User:{user.ObjectId}";
            _cacheProvider?.Add(cacheKey, user);
        }

        /// <summary>
        /// Update a user
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        /// <remarks>Will update the cache provider if implemented</remarks>
        public void UpdateUser(IUser user)
        {
            UpdateUserAsync(user).Wait();
        }

        /// <summary>
        /// Reset a users password and forces change on next login. Generates a random password.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="randomPasswordLength"></param>
        /// <returns>The password set</returns>
        /// <remarks>Requires Directory.AccessAsUser.All permissions and that the current user is a user</remarks>
        public async Task<string> ResetUserPasswordAsync(IUser user, int randomPasswordLength = 16)
        {

            PasswordProfile passwordProfile = new PasswordProfile
            {
                Password = GetRandomString(16),
                ForceChangePasswordNextLogin = true
            };
            user.PasswordProfile = passwordProfile;
            await user.UpdateAsync();
            return passwordProfile.Password;
        }

        /// <summary>
        /// Reset a users password and forces change on next login. Generates a random password.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="randomPasswordLength"></param>
        /// <returns>The password set</returns>
        /// <remarks>Requires Directory.AccessAsUser.All permissions and that the current user is a user</remarks>
        public string ResetUserPassword(IUser user, int randomPasswordLength = 16)
        {
            return ResetUserPasswordAsync(user, randomPasswordLength).Result;
        }

        /// <summary>
        /// Changes a users password
        /// </summary>
        /// <param name="user"></param>
        /// <param name="currentPassword"></param>
        /// <param name="newPassword"></param>
        /// <returns></returns>
        public async Task ChangeUserPasswordAsync(IUser user, string currentPassword, string newPassword)
        {
            await user.ChangePasswordAsync(currentPassword, newPassword);
        }

        /// <summary>
        /// Changes a users password
        /// </summary>
        /// <param name="user"></param>
        /// <param name="currentPassword"></param>
        /// <param name="newPassword"></param>
        /// <returns></returns>
        public void ChangeUserPasswordUser(IUser user, string currentPassword, string newPassword)
        {
            ChangeUserPasswordAsync(user, currentPassword, newPassword).Wait();
        }

        /// <summary>
        /// Updates a users photo
        /// </summary>
        /// <param name="user">User from AAD</param>
        /// <param name="imageStream">Max file size is 100KB</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">The user must already be saved to the AAD and should contain an Object Id</exception>
        /// <exception cref="ArgumentException">The image file must be less than 100KB</exception>
        /// <remarks>Active directory images should be less than 100KB and should also have a aspect ratio of 1:1 (square)</remarks>
        public async Task UpdateUserPhotoAsync(IUser user, Stream imageStream)
        {
            if (user.ObjectId == null)
            {
                throw new ArgumentException("The user must already be saved to the AAD and should contain an Object Id", nameof(user));
            }

            if (imageStream.Length > 100000)
            {
                throw new ArgumentException("The image file must be less than 100KB");
            }

            await user.ThumbnailPhoto.UploadAsync(imageStream, "application/image");
        }

        /// <summary>
        /// Updates a users photo
        /// </summary>
        /// <param name="user">User from AAD</param>
        /// <param name="imageStream">Max file size is 100KB</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">The user must already be saved to the AAD and should contain an Object Id</exception>
        /// <exception cref="ArgumentException">The image file must be less than 100KB</exception>
        /// <remarks>Active directory images should be less than 100KB and should also have a aspect ratio of 1:1 (square)</remarks>
        public void UpdateUserPhoto(IUser user, Stream imageStream)
        {
            UpdateUserPhotoAsync(user, imageStream).Wait();
        }

        /// <summary>
        /// Gets the Users thumbnail photo from AAD
        /// </summary>
        /// <param name="user"></param>
        /// <returns>Memory Stream</returns>
        public async Task<MemoryStream> GetUserPhotoAsync(IUser user)
        {
            var photo = await user.ThumbnailPhoto.DownloadAsync();
            MemoryStream ms = new MemoryStream();
            await photo.Stream.CopyToAsync(ms);
            return ms;
        }

        /// <summary>
        /// Gets the Users thumbnail photo from AAD
        /// </summary>
        /// <param name="user"></param>
        /// <returns>Memory Stream</returns>
        public MemoryStream GetUserPhoto(IUser user)
        {
            return GetUserPhotoAsync(user).Result;
        }


        /// <summary>
        /// Deletes a user
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        /// <remarks>You can only delete a user using the a User context with the necessary permissions. Will remove user object from cache provider if exists</remarks>
        public async Task DeleteUserAsync(IUser user)
        {
            if (_userConfig == null)
            {
                throw new ArgumentException("The Delete User operation requires the Active Directory Client to be as a User context. Please use the User config constructor.");
            }

            await user.DeleteAsync();

            //Remove user object from cache
            string cacheKey = $"{CachePrefix}:User:{user.ObjectId}";
            if (_cacheProvider != null && _cacheProvider.Exist(cacheKey))
            {
                _cacheProvider.Remove(cacheKey);
            }

        }

        /// <summary>
        /// Deletes a user
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        /// You can only delete a user using the a User context with the necessary permissions.
        public void DeleteUser(IUser user)
        {
            DeleteUserAsync(user).Wait();
        }

        /// <summary>
        /// Disables an account
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public async Task DisableUserAsync(IUser user)
        {
            user.AccountEnabled = false;
            await user.UpdateAsync();
        }

        /// <summary>
        /// Disables an account
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public void DisableUser(IUser user)
        {
            DisableUserAsync(user).Wait();
        }

        /// <summary>
        /// Get the AAD User by Object Id
        /// </summary>
        /// <param name="aadObjectId">AAD User Object Id (nameidentifier claim)</param>
        /// <remarks>Will use Cache Provider if implemented</remarks>
        public async Task<IUser> GetUserAsync(string aadObjectId)
        {
            IUserCollection userCollection = AADClient.Users;

            IUser user;
            string cacheKey = $"{CachePrefix}:User:{aadObjectId}";

            if (_cacheProvider != null && _cacheProvider.Exist(cacheKey))
            {
                user = _cacheProvider.Get<IUser>(cacheKey);
            }
            else
            {
                user = await userCollection.Where(u => u.ObjectId == aadObjectId).ExecuteSingleAsync();
                if (user != null)
                    _cacheProvider?.Add(cacheKey, user);
            }

            return user;
        }


        /// <summary>
        /// Get the AAD User by Object Id
        /// </summary>
        /// <param name="aadObjectId">AAD User Object Id (nameidentifier claim)</param>
        public IUser GetUser(string aadObjectId)
        {
            var user = GetUserAsync(aadObjectId).Result;
            return user;
        }

        /// <summary>
        /// Finds users based on a search string.
        /// Searches userPrincipalName, displayName, giveName, surname
        /// </summary>
        /// <param name="searchString"></param>
        /// <param name="take">The number of records to return. Default is 20</param>
        /// <returns></returns>
        /// <remarks>Requires minimum of User.ReadBasic.All</remarks>
        public async Task<List<IUser>> FindUsersAsync(string searchString, int take = 20)
        {
            IUserCollection userCollection = AADClient.Users;
            var searchResults = await userCollection.Where(user =>
                user.UserPrincipalName.StartsWith(searchString) ||
                user.DisplayName.StartsWith(searchString) ||
                user.GivenName.StartsWith(searchString) ||
                user.Surname.StartsWith(searchString)).Take(take).ExecuteAsync();
            var usersList = searchResults.CurrentPage.ToList();

            return usersList;

        }

        /// <summary>
        /// Finds users based on a search string.
        /// Searches userPrincipalName, displayName, giveName, surname
        /// </summary>
        /// <param name="searchString"></param>
        /// <param name="take">The number of records to return. Default is 20</param>
        /// <returns></returns>
        /// <remarks>Requires minimum of User.ReadBasic.All</remarks>
        public List<IUser> FindUsers(string searchString, int take = 20)
        {
            var userList = FindUsersAsync(searchString, take).Result;
            return userList;
        }


        /// <summary>
        /// Get a Users group collecion (MemberOf)
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public async Task<List<Group>> GetUsersGroupsAsync(User user)
        {
            IUserFetcher userFetcher = user;
            IPagedCollection<IDirectoryObject> pagedCollection = await userFetcher.MemberOf.ExecuteAsync();

            List<IDirectoryObject> directoryObjects = pagedCollection.CurrentPage.ToList();
            List<Group> usersGroups = new List<Group>();

            do
            {
                foreach (var directoryObject in directoryObjects)
                {
                    if (directoryObject is Group)
                        usersGroups.Add(directoryObject as Group);
                }
                pagedCollection = pagedCollection.MorePagesAvailable ? await pagedCollection.GetNextPageAsync() : null;
            } while (pagedCollection != null);

            return usersGroups;
        }

        /// <summary>
        /// Get a Users group collecion (MemberOf)
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public List<Group> GetUsersGroups(User user)
        {
            var usersGroups = GetUsersGroupsAsync(user).Result;
            return usersGroups;
        }

        /// <summary>
        /// Checks if a user is a member of a group
        /// </summary>
        /// <param name="user"></param>
        /// <param name="groupName"></param>
        /// <returns></returns>
        public async Task<bool> IsInGroupAsync(User user, string groupName)
        {
            string cacheKey = $"{CachePrefix}:User:{user.ObjectId}:IsInGroup:{groupName}";
            if (_cacheProvider != null && _cacheProvider.Exist(cacheKey))
            {
                return true;
            }

            var groups = await GetUsersGroupsAsync(user);
            if (groups.Any(g => g.DisplayName.ToLower() == groupName.ToLower()))
            {
                _cacheProvider?.Add(cacheKey, true);

                return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if a user is a member of a group
        /// </summary>
        /// <param name="user"></param>
        /// <param name="groupName"></param>
        /// <returns></returns>
        public bool IsInGroup(User user, string groupName)
        {
            return IsInGroupAsync(user, groupName).Result;
        }

        /// <summary>
        /// Get a Users Role collecion (MemberOf)
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public async Task<List<DirectoryRole>> GetUsersRolesAsync(User user)
        {
            IUserFetcher userFetcher = user;
            IPagedCollection<IDirectoryObject> pagedCollection = await userFetcher.MemberOf.ExecuteAsync();

            List<IDirectoryObject> directoryObjects = pagedCollection.CurrentPage.ToList();
            List<DirectoryRole> usersRoles = new List<DirectoryRole>();

            do
            {
                foreach (var directoryObject in directoryObjects)
                {
                    if (directoryObject is DirectoryRole)
                        usersRoles.Add(directoryObject as DirectoryRole);
                }
                pagedCollection = pagedCollection.MorePagesAvailable ? await pagedCollection.GetNextPageAsync() : null;
            } while (pagedCollection != null);

            return usersRoles;
        }

        /// <summary>
        /// Get a Users Role collecion (MemberOf)
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public List<DirectoryRole> GetUsersRoles(User user)
        {
            return GetUsersRolesAsync(user).Result;
        }

        /// <summary>
        /// Checks if a user has a specific role
        /// </summary>
        /// <param name="user"></param>
        /// <param name="roleName"></param>
        /// <returns></returns>
        public async Task<bool> HasRoleAsync(User user, string roleName)
        {
            string cacheKey = $"{CachePrefix}:User:{user.ObjectId}:HasRole:{roleName}";
            if (_cacheProvider != null && _cacheProvider.Exist(cacheKey))
            {
                return true;
            }

            var roles = await GetUsersRolesAsync(user);
            if (roles.Any(r => r.DisplayName.ToLower() == roleName.ToLower()))
            {
                _cacheProvider?.Add(cacheKey, true);

                return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if a user has a specific role
        /// </summary>
        /// <param name="user"></param>
        /// <param name="roleName"></param>
        /// <returns></returns>
        public bool HasRole(User user, string roleName)
        {
            return HasRoleAsync(user, roleName).Result;
        }

        #endregion

        #region Group Operations

        /// <summary>
        /// Get all AD Groups
        /// </summary>
        /// <param name="take">The number of records to return.</param>
        /// <returns></returns>
        public async Task<IPagedCollection<IGroup>> GetUserGroupsAsync(int take = 20)
        {
            var groups = await AADClient.Groups.Take(take).ExecuteAsync();
            return groups;
        }

        /// <summary>
        /// Get all AD Groups as a list
        /// </summary>
        /// <param name="take"></param>
        /// <returns></returns>
        public List<IGroup> GetUserGroups(int take = 20)
        {
            var groupsList = GetUserGroupsAsync(take).Result.CurrentPage.ToList();
            return groupsList;
        }


        /// <summary>
        /// Add a user to a group
        /// </summary>
        /// <param name="group"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public async Task AddUserToGroupAsync(Group group, IUser user)
        {
            group.Members.Add(user as DirectoryObject);
            await group.UpdateAsync();

            _cacheProvider?.Add($"{CachePrefix}:User:{user.ObjectId}:IsInGroup:{group.DisplayName}", true);

        }

        /// <summary>
        /// Add a user to a group
        /// </summary>
        /// <param name="group"></param>
        /// <param name="user"></param>
        public void AddUserToGroup(Group group, IUser user)
        {
            AddUserToGroupAsync(group, user).Wait();
        }

        /// <summary>
        /// Removes a user from a group
        /// </summary>
        /// <param name="group"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public async Task RemoveUserFromGroupAsync(Group group, IUser user)
        {
            if (group.ObjectId != null)
            {
                group.Members.Remove(user as DirectoryObject);
                await group.UpdateAsync();

                _cacheProvider?.Remove($"{CachePrefix}:User:{user.ObjectId}:IsInGroup:{group.DisplayName}");

            }
        }

        /// <summary>
        /// Removes a user from a group
        /// </summary>
        /// <param name="group"></param>
        /// <param name="user"></param>
        public void RemoveUserFromGroup(Group group, IUser user)
        {
            RemoveUserFromGroupAsync(group, user).Wait();
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Returns a random string of upto 32 characters.
        /// </summary>
        /// <param name="length">A value from 1 to 32. If a number larger than 32, then 32 is used.</param>
        /// <returns>String of upto 32 characters.</returns>
        public string GetRandomString(int length = 32)
        {
            if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));
            //because GUID can't be longer than 32
            return Guid.NewGuid().ToString("N").Substring(0, length > 32 ? 32 : length);
        }

        #endregion


        #region Configuration Sub-classes

        public abstract class AADGraphHandlerConfigurationBase
        {
            /// <summary>
            /// The Azure Active Directory Tenant Id. 
            /// This is the same as the Directory ID (Can be found in the AD Blade > Properties)
            /// e.g. '80911c20-47r1-42a8-b888-8a3f876abcb9'
            /// </summary>
            public string TenantId { get; set; }

            /// <summary>
            /// The Azure Active Directory Tenant Name
            /// This is the same as the Directory Name (Can be found in the AD Blade > Properties)
            /// This does not include the '.onmicrosoft.com' usually!
            /// </summary>
            public string TenantDisplayName { get; set; }

            /// <summary>
            /// The root of the graph service endpoint. e.g. https://graph.windows.net/
            /// </summary>
            public Uri GraphServiceRootUri { get; set; }

            /// <summary>
            /// The root of the Tenants graph service endpoint. e.g. https://graph.windows.net/{tenantId}
            /// </summary>
            public Uri TenantGraphServiceRootUri => new Uri(GraphServiceRootUri, TenantId);

            /// <summary>
            /// This is the root of the login Uri e.g. 'https://login.microsoftonline.com/'
            /// </summary>
            public Uri AuthorityServiceRootUri { get; set; }

            /// <summary>
            /// Cache provider [Optional]
            /// </summary>
            /// <remarks>Cache keys are as follows:
            /// IUser - "HMSAAD:User:{AAD ObjectId}"
            /// IsInGroup - "HMSAAD:User:{AAD ObjectId}:IsInGroup:{GroupName}
            /// HasRole - "HMSAAD:User:{AAD ObjectId}:HasRole:{RoleName}
            /// </remarks>
            public ICacheProvider CacheProvider { get; set; }


        }

        /// <summary>
        /// Configuration package for the Graph client and App authentication.
        /// Assemble and pass to the AADGraphHandler constructor
        /// </summary>
        public class AADGraphHandlerConfigurationForApp : AADGraphHandlerConfigurationBase
        {

            /// <summary>
            /// The root of the Tenants authorization Uri e.g. 'https://login.microsoftonline.com/{tenantId}'
            /// </summary>
            public Uri TenantAuthorityServiceUri => new Uri(AuthorityServiceRootUri, TenantId);

            /// <summary>
            /// Registered application Id
            /// </summary>
            public string AppClientId { get; set; }

            /// <summary>
            /// Registered application Secret
            /// </summary>
            public string AppClientSecret { get; set; }
        }

        /// <summary>
        /// Configuration package for the Graph client and User authentication.
        /// Assemble and pass to the AADGraphHandler constructor
        /// </summary>
        public class AADGraphHandlerConfigurationForUser : AADGraphHandlerConfigurationBase
        {
            /// <summary>
            /// The root of the User authorization Uri e.g. 'https://login.microsoftonline.com/common/'
            /// </summary>
            public Uri TenantAuthorityServiceUri => new Uri(AuthorityServiceRootUri, "common/");

            /// <summary>
            /// Redirect Uri
            /// </summary>
            public Uri RedirectUri { get; set; }

            /// <summary>
            /// AAD Application Client Id - Native
            /// </summary>
            public string AppClientId { get; set; }

            /// <summary>
            /// [Optional] If a username is specified then the user prompt is restrict to just this login. 
            /// If the user attempts to use a different account then an error will occur.
            /// </summary>
            public string Username { get; set; }
        }

        public interface ICacheProvider
        {
            /// <summary>
            /// Removes an object from the cache
            /// </summary>
            /// <param name="key"></param>
            /// <returns></returns>
            bool Remove(string key);

            /// <summary>
            /// Adds or overwrites an object to the cache
            /// </summary>
            /// <param name="key">The key should be unique and without spaces. Separate details using colons</param>
            /// <param name="value"></param>
            /// <param name="expiry">Expiry timeout for this value. If not given then platform default is used</param>
            /// <returns></returns>
            bool Add(string key, object value, TimeSpan? expiry = null);

            /// <summary>
            /// Checks if a key exists
            /// </summary>
            /// <param name="key"></param>
            /// <returns></returns>
            bool Exist(string key);

            /// <summary>
            /// Fetches a cached object if exists
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="key"></param>
            /// <returns></returns>
            T Get<T>(string key);
        }

        #endregion

        #region IDisposable

        private bool _disposed = false;

        //Finalize method for the object, will call Dispose for us
        //to clean up the resources if the user has not called it
        ~AADGraphHandler()
        {
            //Indicate that the GC called Dispose, not the user
            Dispose(false);
        }

        //This is the public method, it will HOPEFULLY but
        //not always be called by users of the class
        public void Dispose()
        {
            //indicate this was NOT called by the Garbage collector
            Dispose(true);

            //Now we have disposed of all our resources, the GC does not
            //need to do anything, stop the finalizer being called
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            //Check to see if we have already disposed the object
            //this is necessary because we should be able to call
            //Dispose multiple times without throwing an error
            if (!_disposed)
            {
                if (disposing)
                {
                    //clean up managed resources
                    _aadClient = null;
                }

                //clear up any unmanaged resources - this is safe to
                //put outside the disposing check because if the user
                //called dispose we want to also clean up unmanaged
                //resources, if the GC called Dispose then we only
                //want to clean up managed resources
            }
        }


        #endregion

    }
}

// Copyright (c) 2017 Hard Medium Soft Ltd.  
// 
// Author: Nicholas Rogoff
// Created: 2017 - 02 - 27
// 
// Project: hms.Common.Azure.AADGraphHandler
// Filename: IAADGraphHandler.cs


using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.ActiveDirectory.GraphClient;
using Microsoft.Azure.ActiveDirectory.GraphClient.Extensions;

namespace hms.Common.Azure.AADGraphHandler
{
    /// <summary>
    /// Azure Active Directory Client and Graph Handler
    /// </summary>
    public interface IAADGraphHandler : IDisposable
    {
        /// <summary>
        /// The Active Directory Client created
        /// </summary>
        ActiveDirectoryClient AADClient { get; }
        string TokenForApplication { get; set; }
        string TokenForUser { get; set; }

        #region Tenant Operations

        /// <summary>
        /// Get the Tenant Details
        /// </summary>
        /// <returns></returns>
        /// <remarks>The following section may be run by any user, as long as the app
        /// has been granted the minimum of User.Read (and User.ReadWrite to update photo)
        /// and User.ReadBasic.All scope permissions. Directory.ReadWrite.All
        /// or Directory.AccessAsUser.All will also work, but are much more privileged.
        /// </remarks>
        Task<ITenantDetail> GetTenantDetailsAsync();

        /// <summary>
        /// get the default domain name. This can be the same as the initial domain, but may also be a  custom domain.
        /// </summary>
        /// <returns></returns>
        /// <remarks>see remarks for GetTenantDetailsAsync</remarks>
        string GetTenantDefaultDomain();

        /// <summary>
        /// Get the initial domain name. Usually the .onMicrosoft.com domain.
        /// </summary>
        /// <returns></returns>
        /// <remarks>see remarks for GetTenantDetailsAsync</remarks>
        string GetTenantInitialDomain();

        #endregion

        #region User Operations

        /// <summary>
        /// Get the currently signed in User
        /// </summary>
        /// <returns></returns>
        Task<User> GetSignedInUserAsync();

        /// <summary>
        /// Get the currently signed in User
        /// </summary>
        /// <returns></returns>
        User GetSignedInUser();

        /// <summary>
        /// Creates a new user account
        /// </summary>
        /// <param name="newUser"></param>
        /// <returns></returns>
        /// <remarks>Requires Directory.ReadWrite.All or Directory.AccessAsUser.All, and the signed in user
        /// must be a privileged user (like a company or user admin)</remarks>
        Task<bool> CreateNewUserAsync(User newUser);

        /// <summary>
        /// Creates a new user account
        /// </summary>
        /// <param name="newUser"></param>
        /// <returns></returns>
        /// <remarks>Requires Directory.ReadWrite.All or Directory.AccessAsUser.All, and the signed in user
        /// must be a privileged user (like a company or user admin)</remarks>
        bool CreateNewUser(User newUser);

        /// <summary>
        /// Update a user
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        Task UpdateUserAsync(IUser user);

        /// <summary>
        /// Update a user
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        void UpdateUser(IUser user);

        /// <summary>
        /// Reset a users password and forces change on next login. Generates a random password.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="randomPasswordLength">Max of 32, default = 16</param>
        /// <returns>The password set</returns>
        Task<string> ResetUserPasswordAsync(IUser user, int randomPasswordLength = 16);

        /// <summary>
        /// Reset a users password and forces change on next login. Generates a random password.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="randomPasswordLength">Max of 32, default = 16</param>
        /// <returns>The password set</returns>
        /// <remarks>requires Directory.AccessAsUser.All and that the current user is a user, helpdesk or company admin</remarks>
        string ResetUserPassword(IUser user, int randomPasswordLength = 16);

        /// <summary>
        /// Changes a users password
        /// </summary>
        /// <param name="user"></param>
        /// <param name="currentPassword"></param>
        /// <param name="newPassword"></param>
        /// <returns></returns>
        Task ChangeUserPasswordAsync(IUser user, string currentPassword, string newPassword);

        /// <summary>
        /// Changes a users password
        /// </summary>
        /// <param name="user"></param>
        /// <param name="currentPassword"></param>
        /// <param name="newPassword"></param>
        /// <remarks>requires Directory.AccessAsUser.All and that the current user is a user, helpdesk or company admin</remarks>
        void ChangeUserPasswordUser(IUser user, string currentPassword, string newPassword);

        /// <summary>
        /// Updates a users photo
        /// </summary>
        /// <param name="user">User from AAD</param>
        /// <param name="imageStream">Max file size is 100KB</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">The user must already be saved to the AAD and should contain an Object Id</exception>
        /// <exception cref="ArgumentException">The image file must be less than 100KB</exception>
        /// <remarks>Active directory images should be less than 100KB and should also have a aspect ratio of 1:1 (square)</remarks>
        Task UpdateUserPhotoAsync(IUser user, Stream imageStream);

        /// <summary>
        /// Updates a users photo
        /// </summary>
        /// <param name="user">User from AAD</param>
        /// <param name="imageStream">Max file size is 100KB</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">The user must already be saved to the AAD and should contain an Object Id</exception>
        /// <exception cref="ArgumentException">The image file must be less than 100KB</exception>
        /// <remarks>Active directory images should be less than 100KB and should also have a aspect ratio of 1:1 (square)</remarks>
        void UpdateUserPhoto(IUser user, Stream imageStream);

        /// <summary>
        /// Gets the Users thumbnail photo from AAD
        /// </summary>
        /// <param name="user"></param>
        /// <returns>Memory Stream</returns>
        Task<MemoryStream> GetUserPhotoAsync(IUser user);

        /// <summary>
        /// Gets the Users thumbnail photo from AAD
        /// </summary>
        /// <param name="user"></param>
        /// <returns>Memory Stream</returns>
        MemoryStream GetUserPhoto(IUser user);

        /// <summary>
        /// Deletes a user
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        /// <remarks>You can only delete a user using the a User context with the necessary permissions.</remarks>
        Task DeleteUserAsync(IUser user);

        /// <summary>
        /// Deletes a user
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        /// <remarks>You can only delete a user using the a User context with the necessary permissions.</remarks>
        void DeleteUser(IUser user);

        /// <summary>
        /// Disables a user
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        Task DisableUserAsync(IUser user);

        /// <summary>
        /// Disables an account
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        void DisableUser(IUser user);

        /// <summary>
        /// Get the AAD User by Object Id
        /// </summary>
        /// <param name="aadObjectId">AAD User Object Id (nameidentifier claim)</param>
        Task<IUser> GetUserAsync(string aadObjectId);

        /// <summary>
        /// Get the AAD User by Object Id
        /// </summary>
        /// <param name="aadObjectId">AAD User Object Id (nameidentifier claim)</param>
        IUser GetUser(string aadObjectId);

        /// <summary>
        /// Finds users based on a search string.
        /// Searches userPrincipalName, displayName, giveName, surname
        /// </summary>
        /// <param name="searchString"></param>
        /// <param name="take">The number of records to return. Default is 20</param>
        /// <returns></returns>
        /// <remarks>Requires minimum of User.ReadBasic.All</remarks>
        Task<List<IUser>> FindUsersAsync(string searchString, int take = 20);

        /// <summary>
        /// Finds users based on a search string.
        /// Searches userPrincipalName, displayName, giveName, surname
        /// </summary>
        /// <param name="searchString"></param>
        /// <param name="take">The number of records to return. Default is 20</param>
        /// <returns></returns>
        /// <remarks>Requires minimum of User.ReadBasic.All</remarks>
        List<IUser> FindUsers(string searchString, int take = 20);


        /// <summary>
        /// Get a Users group collecion (MemberOf)
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        Task<List<Group>> GetUsersGroupsAsync(User user);

        /// <summary>
        /// Get a Users group collecion (MemberOf)
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        List<Group> GetUsersGroups(User user);

        /// <summary>
        /// Checks if a user is a member of a group
        /// </summary>
        /// <param name="user"></param>
        /// <param name="groupName"></param>
        /// <returns></returns>
        Task<bool> IsInGroupAsync(User user, string groupName);

        /// <summary>
        /// Checks if a user is a member of a group
        /// </summary>
        /// <param name="user"></param>
        /// <param name="groupName"></param>
        /// <returns></returns>
        bool IsInGroup(User user, string groupName);

        /// <summary>
        /// Get a Users Role collecion (MemberOf)
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        Task<List<DirectoryRole>> GetUsersRolesAsync(User user);

        /// <summary>
        /// Get a Users Role collecion (MemberOf)
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        List<DirectoryRole> GetUsersRoles(User user);

        /// <summary>
        /// Checks if a user has a specific role
        /// </summary>
        /// <param name="user"></param>
        /// <param name="roleName"></param>
        /// <returns></returns>
        Task<bool> HasRoleAsync(User user, string roleName);

        /// <summary>
        /// Checks if a user has a specific role
        /// </summary>
        /// <param name="user"></param>
        /// <param name="roleName"></param>
        /// <returns></returns>
        bool HasRole(User user, string roleName);

        #endregion

        #region Group Operations

        /// <summary>
        /// Gets all User Groups in Tenant
        /// </summary>
        /// <param name="take">the number to take per page. Default=20</param>
        /// <returns></returns>
        Task<IPagedCollection<IGroup>> GetUserGroupsAsync(int take = 20);

        /// <summary>
        /// Gets all User Groups in Tenant
        /// </summary>
        /// <param name="take">the number to take per page. Default=20</param>
        /// <returns></returns>
        List<IGroup> GetUserGroups(int take = 20);

        /// <summary>
        /// Add a user to a group
        /// </summary>
        /// <param name="group"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        Task AddUserToGroupAsync(Group group, IUser user);

        /// <summary>
        /// Add a user to a group
        /// </summary>
        /// <param name="group"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        void AddUserToGroup(Group group, IUser user);

        /// <summary>
        /// Removes a user from a group
        /// </summary>
        /// <param name="group"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        Task RemoveUserFromGroupAsync(Group group, IUser user);

        /// <summary>
        /// Removes a user from a group
        /// </summary>
        /// <param name="group"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        void RemoveUserFromGroup(Group group, IUser user);

        #endregion

        #region Utilities

        /// <summary>
        /// Returns a random string of upto 32 characters.
        /// </summary>
        /// <param name="length">A value from 1 to 32. If a number larger than 32, then 32 is used.</param>
        /// <returns>String of upto 32 characters.</returns>
        string GetRandomString(int length = 32);

        #endregion

    }
}
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using FluentAssertions;
using hms.Common.Azure.AADGraphHandler;
using Microsoft.Azure.ActiveDirectory.GraphClient;
using NUnit.Framework;

namespace hms.Common.Azure.AADTests
{
    [TestFixture]
    public class AADGraphHandlerTests
    {
        #region Test Parameters - Configure for your AD

        //NOTE: Change the following settings in the app.config to test against your Azure AD or B2C
        private static readonly string TenantId = ConfigurationManager.AppSettings["aad:TenantId"];
        private static readonly string TenantDisplayName = ConfigurationManager.AppSettings["aad:TenantDisplayName"];
        private static readonly string TenantDefaultDomain = ConfigurationManager.AppSettings["aad:TenantDefaultDomain"];
        private static readonly string TenantInitialDomainName = ConfigurationManager.AppSettings["aad:TenantInitialDomainName"];

        private static readonly string ApiAppClientId = ConfigurationManager.AppSettings["aad:ApiAppClientId"];
        private static readonly string ApiAppClientSecret = ConfigurationManager.AppSettings["aad:ApiAppClientSecret"]; 

        private static readonly string NativeAppClientId = ConfigurationManager.AppSettings["aad:NativeAppClientId"]; //used for as user tests

        private static readonly string GraphServiceRootUri = ConfigurationManager.AppSettings["aad:GraphServiceRootUri"];
        private static readonly string AuthorityServiceRootUri = ConfigurationManager.AppSettings["aad:AuthorityServiceRootUri"];
        private static readonly string RedirectUri = ConfigurationManager.AppSettings["aad:RedirectUri"];

        //NOTE: Integration Test Admin Account details. This is required to run the delete user account tests
        //NOTE: and will create a user prompt that will need the password
        private static readonly string IntegrationUserAdminUserName = ConfigurationManager.AppSettings["aad:IntegrationUserAdminUserName"];

        #endregion

        private static AADGraphHandler.AADGraphHandler.AADGraphHandlerConfigurationForApp _appConfig;
        private static IAADGraphHandler _aadAppGraphHandler;

        /// <summary>
        /// Users created by create user tests and then reused
        /// </summary>
        private readonly List<IUser> _intTestUsersCreated = new List<IUser>();


        #region Additional test attributes

        // You can use the following additional attributes as you support your tests:

        // Use to run code Before any tests in a class have run
        [OneTimeSetUp]
        public static void TestFixtureSetup()
        {

            // Setup App AAD Client
            _appConfig = new AADGraphHandler.AADGraphHandler.AADGraphHandlerConfigurationForApp
            {
                TenantId = TenantId,
                GraphServiceRootUri = new Uri(GraphServiceRootUri),
                AuthorityServiceRootUri = new Uri(AuthorityServiceRootUri),
                TenantDisplayName = TenantDisplayName,
                AppClientId = ApiAppClientId,
                AppClientSecret = ApiAppClientSecret
            };
            _aadAppGraphHandler = new AADGraphHandler.AADGraphHandler(_appConfig);
        }

        // Use to run code before each test in the class
        // [SetUp]
        // public void Setup(TestContext testContext) { }

        // Use to run code after each test has run
        // [TearDown]
        // public void TearDown() { }

        // Use to run code afer all tests have run 
        [OneTimeTearDown]
        public void TestFixtureTearDown()
        {
            _appConfig = null;
            _aadAppGraphHandler.Dispose();
            _aadAppGraphHandler = null;
        }

        /// <summary>
        /// Sets up a client as a User
        /// </summary>
        /// <param name="username">The username to pre-populate the login</param>
        private AADGraphHandler.AADGraphHandler SetupAADUserGraphHandler(string username)
        {
            // Setup User AAD Client
            // This is missing the User Account details as this is used once for a normal user 
            // and once as the integration test admin user
            var userConfig = new AADGraphHandler.AADGraphHandler.AADGraphHandlerConfigurationForUser
            {
                TenantId = TenantId,
                GraphServiceRootUri = new Uri(GraphServiceRootUri),
                AuthorityServiceRootUri = new Uri(AuthorityServiceRootUri),
                TenantDisplayName = TenantDisplayName,
                RedirectUri = new Uri(RedirectUri),
                AppClientId = NativeAppClientId,
                Username = username
            };
            return new AADGraphHandler.AADGraphHandler(userConfig);
        }

        #endregion

        //Get Tenant Details
        [Test, Order(10)]
        public void AppGetTenantDetails_OK_Success()
        {
            // ==== Arrange ====


            // ==== Act ====
            var actual = _aadAppGraphHandler.GetTenantDetailsAsync().Result;

            // ==== Assert ====

            actual.DisplayName.Should().Be(TenantDisplayName);
        }


        [Test, Order(20)]
        public void AppGetTenantDefaultDomain_OK_Success()
        {
            // ==== Arrange ====


            // ==== Act ====

            var actual = _aadAppGraphHandler.GetTenantDefaultDomain();

            // ==== Assert ====

            actual.Should().Be(TenantDefaultDomain);
        }


        // Create users

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [Test, Order(30)]
        public void AppCreateUser_OK_Success(int userNo)
        {
            // ==== Arrange ====

            User newuser = new User
            {
                GivenName = "IntegrationTest",
                Surname = $"TestUser{userNo}",
                DisplayName = $"IntegrationTest TestUser{userNo}",
                MailNickname = $"IntegrationTestUser{userNo}",
                UserPrincipalName = $"integration.user{userNo}.{_aadAppGraphHandler.GetRandomString(6)}@{TenantInitialDomainName}",
                AccountEnabled = true,
                PasswordProfile = new PasswordProfile
                {
                    Password = $"P@ssWord{userNo}",
                    ForceChangePasswordNextLogin = true
                },
                UsageLocation = "GB"
            };

            // ==== Act ====

            var actual = _aadAppGraphHandler.CreateNewUserAsync(newuser).Result;

            // ==== Assert ====

            actual.Should().Be(true);
        }


        [TestCase("IntegrationTest", 4)]
        [TestCase("int", 4)]
        [TestCase("Int", 4)]
        [TestCase("testuSe", 3)]
        [TestCase("tesTuse", 3)]
        [TestCase("zz", 0)]
        [Test, Order(40)]
        public void FindUsers_Exists_Success(string searchString, int noExpected)
        {
            // ==== Arrange ====


            // ==== Act ====

            var actual = _aadAppGraphHandler.FindUsers(searchString);

            //store integration users for alter use
            if (actual != null && searchString == "IntegrationTest")
            {
                _intTestUsersCreated.AddRange(actual);
            }

            // ==== Assert ====
            actual.Should().NotBeNull();
            actual.Should().HaveCount(noExpected);
        }


        [Test, Order(50)]
        public void AppGetUser_Exists_Success()
        {
            //// ==== Arrange ====
            var userObjectId = _intTestUsersCreated.First().ObjectId; //first integration user

            //// ==== Act ====

            IUser actualUser = _aadAppGraphHandler.GetUser(userObjectId);

            //// ==== Assert ====

            actualUser.ObjectId.Should().BeEquivalentTo(userObjectId);
        }


        [Test, Order(60)]
        public void AppGetAllGroups_Get_Success()
        {
            // ==== Arrange ====


            // ==== Act ====

            var actual = _aadAppGraphHandler.GetUserGroups();

            // ==== Assert ====
            actual.Should().NotBeNull();
            actual.Should().HaveCount(4);
        }

        [TestCase(1, "Contributors")]
        [TestCase(2, "Editors")]
        [TestCase(3, "Reviewers")]
        [Test, Order(65)]
        public void AppAddUserToGroup_Exists_Success(int userNo, string groupName)
        {
            // ==== Arrange ====

            var user = _aadAppGraphHandler.FindUsers($"TestUser{userNo}").FirstOrDefault();
            var group = (Group)_aadAppGraphHandler.GetUserGroups().FirstOrDefault(g => g.DisplayName == groupName);


            // ==== Act ====
            _aadAppGraphHandler.AddUserToGroup(group, user);
            var actual = _aadAppGraphHandler.GetUsersGroups((User)user);

            // ==== Assert ====

            actual.FirstOrDefault(g => g.DisplayName == groupName).Should().NotBeNull();
        }


        [Test, Order(70)]
        public void GetUsersGroups_Exists_Success()
        {
            // ==== Arrange ====
            var userObjectId = "36c73970-fb70-4809-9ada-d180dcba7afc"; //hardmediumsoft1@gmail.com
            var user = (User) _aadAppGraphHandler.GetUser(userObjectId);


            // ==== Act ====

            var actual = _aadAppGraphHandler.GetUsersGroups(user);

            // ==== Assert ====

            actual.Should().HaveCount(1);
            actual.Should().ContainItemsAssignableTo<Group>().And.ContainSingle(g => g.DisplayName == "Admins", "the single users group should be 'Admins'");
        }

        // Add or remove [TestCase] attributes for multiple cases
        [TestCase(1, "Contributors", true)]
        [TestCase(1, "Editors", false)]
        [TestCase(1, "zzz", false)]
        [TestCase(2, "Editors", true)]
        [TestCase(2, "Contributors", false)]
        [TestCase(3, "Reviewers", true)]
        [TestCase(3, "Contributors", false)]
        [Test, Order(80)]
        public void AppIsInGroup_UserExists_Success(int userNo, string groupName, bool inGroup)
        {
            // ==== Arrange ====

            // ==== Act ====
            var user = (User)_aadAppGraphHandler.FindUsers($"TestUser{userNo}").FirstOrDefault();
            var actual = _aadAppGraphHandler.IsInGroup(user, groupName);

            // ==== Assert ====

            actual.Should().Be(inGroup);
        }



        [Test, Order(90)]
        public void AppDeleteUser_Exists_Exception_NoPermissions()
        {
            // ==== Arrange ====

            var usersToDelete = _aadAppGraphHandler.FindUsers("IntegrationTest");

            // ==== Act ====

            Action act = () => _aadAppGraphHandler.DeleteUser(usersToDelete.First());

            // ==== Assert ====

            act.ShouldThrow<Exception>("Only users with 'User Account Administrator' role and above can delete user accounts");
        }

        //[Ignore("not deleting right now")]
        [Test, Order(100)]
        public void UserDeleteUser_Exists_Success()
        {
            // ==== Arrange ====

            //Get Integration Test Admin User using App Context
            var intAdminUser = _aadAppGraphHandler.FindUsers(IntegrationUserAdminUserName).FirstOrDefault();

            if (intAdminUser != null)
            {
                //Create user context client
                using (var aadUserGraphHandler = SetupAADUserGraphHandler(IntegrationUserAdminUserName))
                {
                    var usersToDelete = aadUserGraphHandler.FindUsers("IntegrationTest");

                    // ==== Act ====

                    foreach (var user in usersToDelete)
                    {
                        if (user.UserPrincipalName.ToLower() != IntegrationUserAdminUserName.ToLower()) //not integration admin user
                        {
                            aadUserGraphHandler.DeleteUser(user);
                        }
                    }

                    // ==== Assert ====
                    //Use App Context client to verify
                    var actual = aadUserGraphHandler.FindUsers("IntegrationTest").Where(u => u.UserPrincipalName.ToLower() != IntegrationUserAdminUserName.ToLower());
                    actual.Should().HaveCount(0);
                }
            }
            else
            {
                Assert.Fail($"Can't find Integration Test Admin user account {IntegrationUserAdminUserName}");
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.Caching;
using FluentAssertions;
using hms.Common.Azure.AADGraphHandler;
using Microsoft.Azure.ActiveDirectory.GraphClient;
using NUnit.Framework;
using NUnit.Framework.Constraints;

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

        private static MemoryCache _memoryCache;
        private static TestCacheProvider _cacheProvider;

        #region Additional test attributes

        // You can use the following additional attributes as you support your tests:

        // Use to run code Before any tests in a class have run
        [OneTimeSetUp]
        public static void TestFixtureSetup()
        {

            _memoryCache = MemoryCache.Default;
            _cacheProvider = new TestCacheProvider(_memoryCache);

            // Setup App AAD Client
            _appConfig = new AADGraphHandler.AADGraphHandler.AADGraphHandlerConfigurationForApp
            {
                TenantId = TenantId,
                GraphServiceRootUri = new Uri(GraphServiceRootUri),
                AuthorityServiceRootUri = new Uri(AuthorityServiceRootUri),
                TenantDisplayName = TenantDisplayName,
                AppClientId = ApiAppClientId,
                AppClientSecret = ApiAppClientSecret,
                CacheProvider = _cacheProvider
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

        
        [Repeat(100)] //this has been tested up to 100,000 repeats.
        [TestCase(6,false)]
        [TestCase(6,true)]
        [TestCase(12,true)]
        [TestCase(12,false)]
        [TestCase(32,true)]
        [TestCase(32,true)]
        [Test, Order(5)]
        public void GetRandomString_Default_Succeed(int length, bool ensureComplexity)
        {
            // ==== Arrange ====

            // ==== Act ====
            
            var actual = _aadAppGraphHandler.GetPasswordString(length, ensureComplexity);

            // ==== Assert ====
            if (ensureComplexity)
            {
                actual.Should().MatchRegex(@"^(?=.*\d)(?=.*[a-z])(?=.*[A-Z]).{5,32}$", "because it should have at least one letter, one number and one capital and no more than 16 chars");
            }
            else
            {
                actual.Length.Should().Be(length);
            }
        }

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

        [Test, Order(20)]
        public void AppGetTenantInitialDomain_OK_Success()
        {
            // ==== Arrange ====

            // ==== Act ====
            var actual = _aadAppGraphHandler.GetTenantInitialDomain();

            // ==== Assert ====
            actual.Should().Be(TenantInitialDomainName);
        }


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
                UserPrincipalName = $"integration.user{userNo}.{_aadAppGraphHandler.GetPasswordString(6)}@{TenantInitialDomainName}",
                AccountEnabled = true,
                PasswordProfile = new PasswordProfile
                {
                    Password = $"P@ssWord{userNo}",
                    ForceChangePasswordNextLogin = true
                },
                UsageLocation = "GB" 
            };

            // ==== Act ====
            var actual = _aadAppGraphHandler.CreateNewUser(newuser);

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

        [Test, Order(52)]
        public void AppGetUser_NotExists_Exception()
        {
            //// ==== Arrange ====
            var userObjectId = "12345678-1234-1234-1234-123456789012"; //Made up object Id

            //// ==== Act ====
            Action act = () => _aadAppGraphHandler.GetUser(userObjectId);

            //// ==== Assert ====
            act.ShouldThrow<AggregateException>("because the user account does not exist");
        }


        [TestCase(1, "London")]
        [TestCase(2, "Paris")]
        [TestCase(3, "New York")]
        [Test, Order(55)]
        public void AppUpdateUser_Exists_Success(int userNo, string city)
        {
            // ==== Arrange ====
            var expectedUser = _aadAppGraphHandler.FindUsers($"TestUser{userNo}").FirstOrDefault();
            if (expectedUser != null) expectedUser.City = city;

            // ==== Act ====
            _aadAppGraphHandler.UpdateUser(expectedUser);

            var actualUser = _aadAppGraphHandler.FindUsers($"TestUser{userNo}").FirstOrDefault();

            // ==== Assert ====
            actualUser?.City.Should().Be(expectedUser?.City);
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


        [Test, Order(85)]
        public void GetUsersRoles_Exists_Success()
        {
            // ==== Arrange ====
            var user = (User)_aadAppGraphHandler.FindUsers(IntegrationUserAdminUserName).FirstOrDefault();

            // ==== Act ====
            var actual = _aadAppGraphHandler.GetUsersRoles(user);

            // ==== Assert ====
            actual.Should().HaveCount(1);
            actual.Should().ContainItemsAssignableTo<DirectoryRole>().And.ContainSingle(g => g.DisplayName == "User Account Administrator", "because the single role should be 'User Account Administrator'");
        }

        [TestCase(1, "Global administrator", false)]
        [TestCase(1, "zzz", false)]
        [TestCase(2, "Global administrator", false)]
        [Test, Order(86)]
        public void AppHasRole_UserExists_Success(int userNo, string roleName, bool inRole)
        {
            // ==== Arrange ====

            // ==== Act ====
            var user = (User)_aadAppGraphHandler.FindUsers($"TestUser{userNo}").FirstOrDefault();
            var actual = _aadAppGraphHandler.HasRole(user, roleName);

            // ==== Assert ====

            actual.Should().Be(inRole);
        }

        [Test, Order(87)]
        public void AppHasRole_AdminUserExists_Success()
        {
            // ==== Arrange ====

            // ==== Act ====
            var user = (User)_aadAppGraphHandler.FindUsers(IntegrationUserAdminUserName).FirstOrDefault();
            var actual = _aadAppGraphHandler.HasRole(user, "User Account Administrator");

            // ==== Assert ====
            actual.Should().BeTrue();
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


        [Test, Order(150)]
        public void GetSignedInUser_SignedIn_Success()
        {
            // ==== Arrange ====

            //Get Integration Test Admin User using App Context
            var intAdminUser = _aadAppGraphHandler.FindUsers(IntegrationUserAdminUserName).FirstOrDefault();

            if (intAdminUser != null)
            {
                //Create user context client
                using (var aadUserGraphHandler = SetupAADUserGraphHandler(IntegrationUserAdminUserName))
                {
                    // ==== Act ====
                    var signedInUser = aadUserGraphHandler.GetSignedInUser();
                    // ==== Assert ====
                    signedInUser.ObjectId.Should().Be(intAdminUser.ObjectId);
                }
            }
        }

        [Test, Order(155)]
        public void UserResetPassword_Exists_Success()
        {
            // ==== Arrange ====
            //Login as admin user

            //Create user context client
            using (var aadUserGraphHandler = SetupAADUserGraphHandler(IntegrationUserAdminUserName))
            {
                var userToReset = aadUserGraphHandler.FindUsers($"TestUser1").FirstOrDefault();
                // ==== Act ====
                var newPassword = aadUserGraphHandler.ResetUserPassword(userToReset);
                // ==== Assert ====
                newPassword.Length.Should().Be(16);
            }
        }


        //[Ignore("not deleting right now")]
        [Test, Order(200)]
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

        //NOTE: This test may fail due to the AAD not having processed the previous delete yet. Run again manually after tests complete.
        [Test, Order(210)]
        public void AppUpdateUser_NotExists_Exception()
        {
            // ==== Arrange ====
            var expectedUser = _intTestUsersCreated.FirstOrDefault();
            if (expectedUser != null) expectedUser.City = "SomeCity";

            // ==== Act ====
            Action act = () => _aadAppGraphHandler.UpdateUser(expectedUser);

            // ==== Assert ====
            act.ShouldThrow<AggregateException>("because the user account has been deleted.");
        }

    }

    /// <summary>
    /// Test Cache provider using the MemoryCache
    /// </summary>
    public class TestCacheProvider:AADGraphHandler.AADGraphHandler.ICacheProvider
    {
        private readonly MemoryCache _memoryCache;

        public TestCacheProvider(MemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }
        public bool Remove(string key)
        {
            var obj = _memoryCache.Remove(key);
            return obj != null;
        }

        public bool Add(string key, object value, TimeSpan? expiry = null)
        {
            if (expiry == null)
            {
                expiry = TimeSpan.FromSeconds(60);
                
            }

            DateTimeOffset expires = DateTimeOffset.UtcNow.Add(expiry.Value);

            return _memoryCache.Add(key, value, expires);
        }

        public bool Exist(string key)
        {
            var obj = _memoryCache.Get(key);
            return obj != null;
        }

        public T Get<T>(string key)
        {
            var value = (T)_memoryCache.Get(key);
            return value;
        }
    }
}
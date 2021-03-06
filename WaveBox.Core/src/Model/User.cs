﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using Cirrious.MvvmCross.Plugins.Sqlite;
using Newtonsoft.Json;
using Ninject;
using WaveBox.Core.Extensions;
using WaveBox.Core.Model;
using WaveBox.Core.Model.Repository;
using WaveBox.Core.Static;

namespace WaveBox.Core.Model {
    public class User : IGroupingItem {
        // PBKDF2 iterations
        public const int HashIterations = 2500;

        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        [JsonProperty("userId")]
        public int? UserId { get; set; }

        [JsonProperty("userName")]
        public string UserName { get; set; }

        // This is only used after test account creation
        [JsonProperty("password"), IgnoreRead, IgnoreWrite]
        public string Password { get; set; }

        [JsonProperty("role")]
        public Role Role { get; set; }

        [JsonProperty("currentSession"), IgnoreRead, IgnoreWrite]
        public Session CurrentSession { get; set; }

        [JsonProperty("sessions"), IgnoreRead, IgnoreWrite]
        public IList<Session> Sessions { get; set; }

        [JsonIgnore]
        public string PasswordHash { get; set; }

        [JsonIgnore]
        public string PasswordSalt { get; set; }

        [JsonIgnore, IgnoreRead, IgnoreWrite]
        public string SessionId { get; set; }

        [JsonProperty("lastfmSession")]
        public string LastfmSession { get; set; }

        [JsonProperty("createTime")]
        public long? CreateTime { get; set; }

        [JsonProperty("deleteTime")]
        public long? DeleteTime { get; set; }

        [JsonIgnore, IgnoreRead, IgnoreWrite]
        public string GroupingName { get { return UserName; } }

        // Verifies that this user object has permission to view a section
        public bool HasPermission(Role role) {
            return this.Role >= role ? true : false;
        }

        public bool UpdateSession(string sessionId) {
            // Update user's session based on its session ID
            Session s = Injection.Kernel.Get<ISessionRepository>().SessionForSessionId(sessionId);

            if (s != null) {
                return s.Update();
            }

            return false;
        }

        public bool DeleteSession(string sessionId) {
            // Delete user's session based on its session ID
            Session s = Injection.Kernel.Get<ISessionRepository>().SessionForSessionId(sessionId);

            // Ensure session actually belongs to this user
            if (s != null && s.UserId == this.UserId) {
                return s.Delete();
            }

            return false;
        }

        public IList<Session> ListOfSessions() {
            ISQLiteConnection conn = null;
            try {
                conn = Injection.Kernel.Get<IDatabase>().GetSqliteConnection();
                return conn.Query<Session>("SELECT RowId AS RowId, * FROM Session WHERE UserId = ?", this.UserId);
            } catch (Exception e) {
                logger.Error(e);
            } finally {
                Injection.Kernel.Get<IDatabase>().CloseSqliteConnection(conn);
            }

            return new List<Session>();
        }

        public bool UpdatePassword(string password) {
            string salt = GeneratePasswordSalt();
            string hash = ComputePasswordHash(password, salt);

            ISQLiteConnection conn = null;
            try {
                conn = Injection.Kernel.Get<IDatabase>().GetSqliteConnection();
                int affected = conn.Execute("UPDATE User SET PasswordHash = ?, PasswordSalt = ? WHERE UserId = ?", hash, salt, this.UserId);

                if (affected > 0) {
                    this.PasswordHash = hash;
                    this.PasswordSalt = salt;

                    return Injection.Kernel.Get<IUserRepository>().UpdateUserCache(this);
                }
            } catch (Exception e) {
                logger.Error(e);
            } finally {
                Injection.Kernel.Get<IDatabase>().CloseSqliteConnection(conn);
            }

            return false;
        }

        public bool UpdateLastfmSession(string sessionKey) {
            ISQLiteConnection conn = null;
            try {
                conn = Injection.Kernel.Get<IDatabase>().GetSqliteConnection();
                int affected = conn.Execute("UPDATE User SET LastfmSession = ? WHERE UserName = ?", sessionKey, UserName);

                if (affected > 0) {
                    this.LastfmSession = sessionKey;

                    return Injection.Kernel.Get<IUserRepository>().UpdateUserCache(this);
                }
            } catch (Exception e) {
                logger.Error(e);
            } finally {
                Injection.Kernel.Get<IDatabase>().CloseSqliteConnection(conn);
            }

            return false;
        }

        // Update a user's username
        public bool UpdateUsername(string username) {
            ISQLiteConnection conn = null;
            try {
                conn = Injection.Kernel.Get<IDatabase>().GetSqliteConnection();
                int affected = conn.Execute("UPDATE User SET UserName = ? WHERE UserId = ?", username, this.UserId);

                if (affected > 0) {
                    this.UserName = username;

                    return Injection.Kernel.Get<IUserRepository>().UpdateUserCache(this);
                }
            } catch (Exception e) {
                logger.Error(e);
            } finally {
                Injection.Kernel.Get<IDatabase>().CloseSqliteConnection(conn);
            }

            return false;
        }

        // Update a user's role
        public bool UpdateRole(Role role) {
            ISQLiteConnection conn = null;
            try {
                conn = Injection.Kernel.Get<IDatabase>().GetSqliteConnection();
                int affected = conn.Execute("UPDATE User SET Role = ? WHERE UserId = ?", role, this.UserId);

                if (affected > 0) {
                    this.Role = role;

                    return Injection.Kernel.Get<IUserRepository>().UpdateUserCache(this);
                }
            } catch (Exception e) {
                logger.Error(e);
            } finally {
                Injection.Kernel.Get<IDatabase>().CloseSqliteConnection(conn);
            }

            return false;
        }

        // Verify password, using timing attack resistant approach
        // Credit to PHP5.5 Password API for this method
        public bool Authenticate(string password) {
            // Compute hash
            string hash = ComputePasswordHash(password, this.PasswordSalt);

            // Ensure hashes are same length
            if (hash.Length != this.PasswordHash.Length) {
                return false;
            }

            // Compare ASCII value of each character, bitwise OR any diff
            int status = 0;
            for (int i = 0; i < hash.Length; i++) {
                status |= ((int)hash[i] ^ (int)this.PasswordHash[i]);
            }

            return status == 0;
        }

        public bool CreateSession(string password, string clientName) {
            // On successful authentication, create session!
            if (this.Authenticate(password)) {
                Session s = Injection.Kernel.Get<ISessionRepository>().CreateSession(Convert.ToInt32(UserId), clientName);
                if (s != null) {
                    SessionId = s.SessionId;
                    return true;
                }
            }

            return false;
        }

        public bool Delete() {
            if (ReferenceEquals(UserId, null)) {
                return true;
            }

            // Delete the user
            ISQLiteConnection conn = null;
            try {
                conn = Injection.Kernel.Get<IDatabase>().GetSqliteConnection();
                int affected = conn.Execute("DELETE FROM User WHERE UserId = ?", UserId);

                if (affected > 0) {
                    // Delete associated sessions
                    Injection.Kernel.Get<ISessionRepository>().DeleteSessionsForUserId((int)UserId);

                    return Injection.Kernel.Get<IUserRepository>().DeleteFromUserCache(this);
                }

                return true;
            } catch (Exception e) {
                logger.Error(e);
            } finally {
                Injection.Kernel.Get<IDatabase>().CloseSqliteConnection(conn);
            }

            return false;
        }

        public override string ToString() {
            return String.Format("[User: UserId={0}, UserName={1}]", this.UserId, this.UserName);
        }

        public static int CompareUsersByName(User x, User y) {
            return StringComparer.OrdinalIgnoreCase.Compare(x.UserName, y.UserName);
        }

        // Compute password hash using PBKDF2
        public static string ComputePasswordHash(string password, string salt, int iterations = HashIterations) {
            // Convert salt to byte array
            byte[] saltBytes = Encoding.UTF8.GetBytes(salt);

            // Hash using PBKDF2 with salt and predefined iterations
            using (Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, iterations)) {
                var key = pbkdf2.GetBytes(64);
                return Convert.ToBase64String(key);
            }
        }

        // Use RNG crypto service to generate random bytes for salt
        public static string GeneratePasswordSalt() {
            // Create byte array to store salt
            byte[] salt = new byte[32];

            // Fill array using RNG
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider()) {
                rng.GetBytes(salt);
            }

            // Return string representation
            return Convert.ToBase64String(salt);
        }
    }
}

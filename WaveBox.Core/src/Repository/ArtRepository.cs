using System;
using Cirrious.MvvmCross.Plugins.Sqlite;
using WaveBox.Core.Extensions;

namespace WaveBox.Core.Model.Repository {
    public class ArtRepository : IArtRepository {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private readonly IDatabase database;

        public ArtRepository(IDatabase database) {
            if (database == null) {
                throw new ArgumentNullException("database");
            }

            this.database = database;
        }

        public Art ArtForId(int artId) {
            return this.database.GetSingle<Art>("SELECT * FROM Art WHERE ArtId = ?", artId);
        }

        public int? ItemIdForArtId(int? artId) {
            if ((object)artId == null) {
                return null;
            }

            int itemId = this.database.GetScalar<int>("SELECT ItemId FROM ArtItem WHERE ArtId = ?", artId);
            return itemId == 0 ? (int?)null : itemId;
        }

        public int? ArtIdForItemId(int? itemId) {
            if ((object)itemId == null) {
                return null;
            }

            int artId = this.database.GetScalar<int>("SELECT ArtId FROM ArtItem WHERE ItemId = ?", itemId);
            return artId == 0 ? (int?)null : artId;
        }

        public int? ArtIdForMd5(string hash) {
            if ((object)hash == null) {
                return null;
            }

            int artId = this.database.GetScalar<int>("SELECT ArtId FROM Art WHERE Md5Hash = ?", hash);
            return artId == 0 ? (int?)null : artId;
        }

        public bool InsertArt(Art art, bool replace = false) {
            return this.database.InsertObject<Art>(art, replace ? InsertType.Replace : InsertType.InsertOrIgnore) > 0;
        }

        public bool UpdateArtItemRelationship(int? artId, int? itemId, bool replace) {
            if (artId == null || itemId == null) {
                return false;
            }

            string type = replace ? "REPLACE" : "INSERT OR IGNORE";
            return this.database.ExecuteQuery(type + " INTO ArtItem (ArtId, ItemId) VALUES (?, ?)", artId, itemId) > 0;
        }

        public bool RemoveArtRelationshipForItemId(int? itemId) {
            if ((object)itemId == null) {
                return false;
            }

            return this.database.ExecuteQuery("DELETE FROM ArtItem WHERE ItemId = ?", itemId) > 0;
        }

        public bool UpdateItemsToNewArtId(int? oldArtId, int? newArtId) {
            if ((object)oldArtId == null || (object)newArtId == null) {
                return false;
            }

            return this.database.ExecuteQuery("UPDATE ArtItem SET ArtId = ? WHERE ArtId = ?", newArtId, oldArtId) > 0;
        }
    }
}

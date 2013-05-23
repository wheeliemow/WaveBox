using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WaveBox.Model;
using WaveBox.Static;
using System.IO;
using TagLib;
using Newtonsoft.Json;
using System.Diagnostics;
using Cirrious.MvvmCross.Plugins.Sqlite;

namespace WaveBox.Model
{
	public class Video : MediaItem
	{
		private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		public static readonly string[] ValidExtensions = { ".m4v", ".mp4", ".mpg", ".mkv", ".avi" };

		[JsonIgnore, IgnoreRead, IgnoreWrite]
		public override ItemType ItemType { get { return ItemType.Video; } }

		[JsonProperty("itemTypeId"), IgnoreRead, IgnoreWrite]
		public override int ItemTypeId { get { return (int)ItemType; } }

		[JsonProperty("width")]
		public int? Width { get; set; }
		
		[JsonProperty("height")]
		public int? Height { get; set; }

		[JsonProperty("aspectRatio")]
		public float? AspectRatio
		{ 
			get 
			{
				if ((object)Width == null || (object)Height == null || Height == 0)
				{
					return null;
				}

				return (float)Width / (float)Height;
			}
		}

		public Video()
		{
		}
		
		public static List<Video> AllVideos()
		{
			ISQLiteConnection conn = null;
			try
			{
				conn = Database.GetSqliteConnection();
				return conn.Query<Video>("SELECT * FROM video");
			}
			catch (Exception e)
			{
				logger.Error(e);
			}
			finally
			{
				conn.Close();
			}

			return new List<Video>();
		}

		public static int CountVideos()
		{
			ISQLiteConnection conn = null;
			try
			{
				conn = Database.GetSqliteConnection();
				return conn.ExecuteScalar<int>("SELECT count(ItemId) FROM video");
			}
			catch (Exception e)
			{
				logger.Error(e);
			}
			finally
			{
				conn.Close();
			}

			return 0;
		}

		public static long TotalVideoSize()
		{
			ISQLiteConnection conn = null;
			try
			{
				conn = Database.GetSqliteConnection();
				return conn.ExecuteScalar<long>("SELECT sum(video_file_size) FROM video");
			}
			catch (Exception e)
			{
				logger.Error(e);
			}
			finally
			{
				conn.Close();
			}

			return 0;
		}

		public static long TotalVideoDuration()
		{
			ISQLiteConnection conn = null;
			try
			{
				conn = Database.GetSqliteConnection();
				return conn.ExecuteScalar<long>("SELECT sum(video_duration) FROM video");
			}
			catch (Exception e)
			{
				logger.Error(e);
			}
			finally
			{
				conn.Close();
			}

			return 0;
		}

		public static List<Video> SearchVideos(string field, string query, bool exact = true)
		{
			if (query == null)
			{
				return new List<Video>();
			}

			// Set default field, if none provided
			if (field == null)
			{
				field = "FileName";
			}

			// Check to ensure a valid query field was set
			if (!new string[] {"ItemId", "FolderId", "Duration", "Bitrate", "FileSize",
				"LastModified", "FileName", "Width", "Height", "FileType",
				"GenereId"}.Contains(field))
			{
				return new List<Video>();
			}

			ISQLiteConnection conn = null;
			try
			{
				conn = Database.GetSqliteConnection();
				if (exact)
				{
					// Search for exact match
					return conn.Query<Video>("SELECT * FROM video WHERE " + field + " = ? ORDER BY FileName", query);
				}
				else
				{
					// Search for fuzzy match (containing query)
					return conn.Query<Video>("SELECT * FROM video WHERE " + field + " LIKE ? ORDER BY FileName", "%" + query + "%");
				}
			}
			catch (Exception e)
			{
				logger.Error(e);
			}
			finally
			{
				conn.Close();
			}

			return new List<Video>();
		}

		public static bool VideoNeedsUpdating(string filePath, int? folderId, out bool isNew, out int? itemId)
		{
			string fileName = Path.GetFileName(filePath);
			long lastModified = System.IO.File.GetLastWriteTime(filePath).ToUniversalUnixTimestamp();
			bool needsUpdating = true;
			isNew = true;
			itemId = null;

			ISQLiteConnection conn = null;
			try
			{
				conn = Database.GetSqliteConnection();
				var result = conn.DeferredQuery<Video>("SELECT * FROM video WHERE FolderId = ? AND FileName = ?", folderId, fileName);

				foreach (Video video in result)
				{
					isNew = false;

					itemId = video.ItemId;
					if (video.LastModified == lastModified)
					{
						needsUpdating = false;
					}

					break;
				}
			}
			catch (Exception e)
			{
				logger.Error(e);
			}
			finally
			{
				conn.Close();
			}

			return needsUpdating;
		}

		public override void InsertMediaItem()
		{
			ISQLiteConnection conn = null;
			try
			{
				conn = Database.GetSqliteConnection();
				conn.InsertLogged(this, InsertType.Replace);
			}
			catch (Exception e)
			{
				logger.Error(e);
			}
			finally
			{
				conn.Close();
			}

			Art.UpdateArtItemRelationship(ArtId, ItemId, true);
			Art.UpdateArtItemRelationship(ArtId, FolderId, false); // Only update a folder art relationship if it has no folder art
		}

		public static int CompareVideosByFileName(Video x, Video y)
		{
			return x.FileName.CompareTo(y.FileName);
		}

		public class Factory
		{
			public Video CreateVideo(int videoId)
			{
				ISQLiteConnection conn = null;
				try
				{
					conn = Database.GetSqliteConnection();
					var result = conn.DeferredQuery<Video>("SELECT * FROM video WHERE ItemId = ?", videoId);

					foreach (Video v in result)
					{
						return v;
					}
				}
				catch (Exception e)
				{
					logger.Error(e);
				}
				finally
				{
					conn.Close();
				}

				return new Video();
			}

			public Video CreateVideo(string filePath, int? folderId, TagLib.File file)
			{
				// We need to check to make sure the tag isn't corrupt before handing off to this method, anyway, so just feed in the tag
				// file that we checked for corruption.
				//TagLib.File file = TagLib.File.Create(fsFile.FullName);

				int? itemId = Item.GenerateItemId(ItemType.Video);
				if (itemId == null)
				{
					return new Video();
				}

				Video video = new Video();
				video.ItemId = itemId;

				FileInfo fsFile = new FileInfo(filePath);
				//TagLib.Tag tag = file.Tag;
				//var lol = file.Properties.Codecs;
				video.FolderId = folderId;

				video.FileType = video.FileType.FileTypeForTagLibMimeType(file.MimeType);

				if (video.FileType == FileType.Unknown)
				{
					if (logger.IsInfoEnabled) logger.Info("\"" + filePath + "\" Unknown file type: " + file.Properties.Description);
				}

				video.Width = file.Properties.VideoWidth;
				video.Height = file.Properties.VideoHeight;
				video.Duration = Convert.ToInt32(file.Properties.Duration.TotalSeconds);
				video.Bitrate = file.Properties.AudioBitrate;
				video.FileSize = fsFile.Length;
				video.LastModified = fsFile.LastWriteTime.ToUniversalUnixTimestamp();
				video.FileName = fsFile.Name;

				// Generate an art id from the embedded art, if it exists
				int? artId = new Art.Factory().CreateArt(file).ArtId;

				// If there was no embedded art, use the folder's art
				artId = (object)artId == null ? Art.ArtIdForItemId(video.FolderId) : artId;

				// Create the art/item relationship
				Art.UpdateArtItemRelationship(artId, video.ItemId, true);

				return video;
			}
		}
	}
}

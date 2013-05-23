using System;
using WaveBox.Static;
using System.Collections.Generic;
using System.Linq;
using Cirrious.MvvmCross.Plugins.Sqlite;

namespace WaveBox.Model
{
	public class Genre
	{
		private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		public int? GenreId { get; set; }

		public string GenreName { get; set; }

		public Genre()
		{

		}

		public void InsertGenre()
		{
			if (GenreName == null)
			{
				// Can't insert a genre with no name
				return;
			}

			int? itemId = Item.GenerateItemId(ItemType.Genre);
			if (itemId == null)
			{
				return;
			}
			
			ISQLiteConnection conn = null;
			try
			{
				// insert the genre into the database
				conn = Database.GetSqliteConnection();
				GenreId = itemId;
				int affected = conn.InsertLogged(this);

				if (affected == 0)
				{
					GenreId = null;
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
		}

		public List<Artist> ListOfArtists()
		{
			ISQLiteConnection conn = null;
			try
			{
				conn = Database.GetSqliteConnection();
				return conn.Query<Artist>("SELECT artist.* " +
				                          "FROM genre " + 
				                          "LEFT JOIN song ON song.GenreId = genre.GenreId " +
				                          "LEFT JOIN artist ON song.ArtistId = artist.ItemId " +
				                          "WHERE genre.GenreId = ? GROUP BY artist.ItemId", GenreId);
			}
			catch (Exception e)
			{
				logger.Error(e);
			}
			finally
			{
				conn.Close();
			}

			return new List<Artist>();
		}

		public List<Album> ListOfAlbums()
		{
			ISQLiteConnection conn = null;
			try
			{
				conn = Database.GetSqliteConnection();
				return conn.Query<Album>("SELECT album.* " +
				                         "FROM genre " + 
				                         "LEFT JOIN song ON song.GenreId = genre.GenreId " +
				                         "LEFT JOIN album ON song.AlbumId = album.ItemId " +
				                         "WHERE genre.GenreId = ? GROUP BY album.ItemId", GenreId);
			}
			catch (Exception e)
			{
				logger.Error(e);
			}
			finally
			{
				conn.Close();
			}
			
			return new List<Album>();
		}

		public List<Song> ListOfSongs()
		{
			ISQLiteConnection conn = null;
			try
			{
				conn = Database.GetSqliteConnection();
				return conn.Query<Song>("SELECT song.*, genre.GenreName " +
				                        "FROM genre " + 
				                        "LEFT JOIN song ON song.GenreId = genre.GenreId " +
				                        "WHERE genre.GenreId = ? GROUP BY song.ItemId", conn);
			}
			catch (Exception e)
			{
				logger.Error(e);
			}
			finally
			{
				conn.Close();
			}
			
			return new List<Song>();
		}

		public List<Folder> ListOfFolders()
		{
			ISQLiteConnection conn = null;
			try
			{
				conn = Database.GetSqliteConnection();
				return conn.Query<Folder>("SELECT folder.* " +
				                          "FROM genre " + 
				                          "LEFT JOIN song ON song.GenreId = genre.GenreId " +
				                          "LEFT JOIN folder ON song.FolderId = folder.FolderId " +
				                          "WHERE genre.GenreId = ? GROUP BY folder.FolderId", GenreId);
			}
			catch (Exception e)
			{
				logger.Error(e);
			}
			finally
			{
				conn.Close();
			}
			
			return new List<Folder>();
		}

		public static List<Genre> AllGenres()
		{
			ISQLiteConnection conn = null;
			try
			{
				conn = Database.GetSqliteConnection();
				return conn.Query<Genre>("SELECT * FROM genre ORDER BY GenreName");
			}
			catch (Exception e)
			{
				logger.Error(e);
			}
			finally
			{
				conn.Close();
			}

			return new List<Genre>();
		}

		public static int CompareGenresByName(Genre x, Genre y)
		{
			return StringComparer.OrdinalIgnoreCase.Compare(x.GenreName, y.GenreName);
		}

		public class Factory
		{
			public Genre CreateGenre(int? genreId)
			{
				if ((object)genreId == null)
				{
					return new Genre();
				}

				ISQLiteConnection conn = null;
				try
				{
					conn = Database.GetSqliteConnection();
					var result = conn.DeferredQuery<Genre>("SELECT * FROM genre WHERE GenreId = ?", genreId);

					foreach (Genre g in result)
					{
						return g;
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

				Genre genre = new Genre();
				genre.GenreId = genreId;
				return genre;
			}

			private static object genreMemCacheLock = new object();
			private static List<Genre> memCachedGenres = new List<Genre>();
			public Genre CreateGenre(string genreName)
			{
				if ((object)genreName == null)
				{
					return new Genre();
				}

				lock (genreMemCacheLock)
				{
					// First check to see if the genre is in mem cache
					Genre genre = (from g in memCachedGenres where g.GenreName.Equals(genreName) select g).FirstOrDefault();

					if ((object)genre != null)
					{
						// We got a match, so use the genre id
						return genre;
					}
					else
					{
						// Retreive the genre id if it exists
						ISQLiteConnection conn = null;
						try
						{
							conn = Database.GetSqliteConnection();
							var result = conn.DeferredQuery<Genre>("SELECT * FROM genre WHERE GenreName = ?", genreName);

							foreach (Genre g in result)
							{
								return g;
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

						// If this genre didn't exist, generate an id and insert it
						Genre genre2 = new Genre();
						genre2.GenreName = genreName;
						genre2.InsertGenre();
						return genre2;
					}
				}
			}
		}
	}
}


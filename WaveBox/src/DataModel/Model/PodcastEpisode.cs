using System;
using System.Data;
using System.IO;
using WaveBox.DataModel.Singletons;
using System.Net;
using System.Web;
using System.Xml;


namespace PodcastParsing
{
    public class PodcastEpisode
    {
        public int? EpisodeId { get; set; }
        public int? PodcastId { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string Subtitle { get; set; }
        public string MediaUrl { get; set; }
        public string FilePath { get; set; }

        private long contentLength, totalBytesRead;
        private Stream response;
        private FileStream s;
        byte[] buf = new byte[8192];

        public PodcastEpisode(XmlNode episode, XmlNamespaceManager mgr, int? podcastId)
        {
            Title = episode.SelectSingleNode("title").InnerText;
            Author = episode.SelectSingleNode("itunes:author", mgr).InnerText;
            Subtitle = episode.SelectSingleNode("itunes:subtitle", mgr).InnerText;
            MediaUrl = episode.SelectSingleNode("enclosure").Attributes["url"].InnerText;
            Console.WriteLine(episode.SelectSingleNode("title").InnerText);
            Console.WriteLine(episode.SelectSingleNode("itunes:author", mgr).InnerText);
            Console.WriteLine(episode.SelectSingleNode("itunes:subtitle", mgr).InnerText);
            Console.WriteLine(episode.SelectSingleNode("enclosure").Attributes["url"].InnerText);
            Console.WriteLine();
        }

        public PodcastEpisode(int podcastId)
        {
            IDbConnection conn = null;
            IDataReader reader = null;
            try
            {
                conn = Database.GetDbConnection();

                IDbCommand q = Database.GetDbCommand("SELECT * FROM podcast WHERE podcast_id = @podcastid", conn);
                q.AddNamedParam("@podcastid", podcastId);
                q.Prepare();
                reader = q.ExecuteReader();

                if (reader.Read())
                {
                    SetPropertiesFromQueryResult(reader);
                }
                else
                {
                    Console.WriteLine("Podcast constructor query returned no results");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("[ARTIST(1)] ERROR: " +  e.ToString());
            }
            finally
            {
                Database.Close(conn, reader);
            }
        }

        public PodcastEpisode()
        {
        }

        public void Delete()
        {
            IDbConnection conn = null;
            IDataReader reader = null;
            try
            {
                conn = Database.GetDbConnection();

                IDbCommand q = Database.GetDbCommand("DELETE FROM podcast WHERE podcast_id = @podcastid", conn);
                q.AddNamedParam("@podcastid", PodcastId);
                q.Prepare();
                q.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                Console.WriteLine("[ARTIST(1)] ERROR: " +  e.ToString());
            }
            finally
            {
                Database.Close(conn, reader);
            }
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }

        public bool IsDownloaded()
        {
            return true;
        }

        public void Download()
        {
            s = new FileStream("lol.mp3", FileMode.Create, FileAccess.ReadWrite);
            var req = (HttpWebRequest)WebRequest.Create(MediaUrl);
            byte[] buf = new byte[8192];
           

            req.BeginGetResponse(result => 
            {
                WebResponse f = req.EndGetResponse(result);
                contentLength = f.ContentLength;
                response = f.GetResponseStream();

                response.BeginRead(buf, 0, 8192, new AsyncCallback(ResponseCallback), null);
            }, 
            null);



        }

        private void ResponseCallback(IAsyncResult asyncResult)
        {
            int bytesRead = response.EndRead(asyncResult);
            if (bytesRead > 0)
            {
                s.Write(buf, 0, bytesRead);
                s.Flush();

                // more to read, so keep going!
                totalBytesRead += bytesRead;

                response.BeginRead(buf, 0, 8192, new AsyncCallback(ResponseCallback), null);
                Console.WriteLine(totalBytesRead + " / " + contentLength);
            }

            // otherwise, we've read all the bytes in the stream, so we're done.
            else
            {
                Console.WriteLine("done!");
                response.Close();
                s.Close();
            }
        }

        public void AddToDatabase()
        {
        }

        public double DownloadProgress()
        {
            return (double)totalBytesRead / (double)contentLength;
        }

        private void SetPropertiesFromQueryResult(IDataReader reader)
        {
            EpisodeId = reader.GetInt32(reader.GetOrdinal("podcast_episode_id"));
            PodcastId = reader.GetInt32(reader.GetOrdinal("podcast_episode_podcast_id"));
            Title = reader.GetString(reader.GetOrdinal("podcast_episode_title"));
            Author = reader.GetString(reader.GetOrdinal("podcast_episode_author"));
            Subtitle = reader.GetString(reader.GetOrdinal("podcast_episode_subtitle"));
            MediaUrl = reader.GetString(reader.GetOrdinal("podcast_episode_media_url"));
        }
    }
}

namespace SpotifyWebRecorder.Forms.UI
{
    /// <summary>
    /// A class containing information for a single Deezer song
    /// </summary>
    public class Mp3Tag
    {
        public string Title { get; set; }
        public string Artist { get; set; }
		public string ID { get; set; }

        public Mp3Tag(string title, string artist)
        {
            Title = title;
            Artist = artist;
        }

        public bool Equals(Mp3Tag obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            if ( this.Artist != obj.Artist )
            {
                return false;
            }
            if ( this.Title != obj.Title )
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Converts this tag into a string
        /// </summary>
        /// <returns>Song in format Artist - Title</returns>
        public override string ToString()
        {
            return this.Artist + " - " + this.Title;
        }

        /// <summary>
        /// Returns a new empty MP3Tag
        /// </summary>
        /// <returns>empty MP3Tag</returns>
        public static Mp3Tag EmptyTag()
        {
            return new Mp3Tag("", "");
        }
    }
}
namespace SpotifyWebRecorder.Forms.UI
{
    public class Mp3Tag
    {
        public string Title { get; set; }
        public string Artist { get; set; }
		public string ID { get; set; }

        public Mp3Tag(string title, string artist)
        {
            Title = title;
            Artist = artist;
			//TrackUri = trackUri;
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

        public override string ToString()
        {
            return this.Artist + " - " + this.Title;
        }

        public static Mp3Tag EmptyTag()
        {
            return new Mp3Tag("", "");
        }
    }
}
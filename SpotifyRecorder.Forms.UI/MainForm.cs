using System.ComponentModel;
using System.Configuration;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;
using System.Runtime.InteropServices;
using CefSharp;
using CefSharp.WinForms;

namespace SpotifyWebRecorder.Forms.UI
{
    public partial class MainForm : Form
    {
		//GeckoWebBrowser browser;
        ChromiumWebBrowser mainBrowser;
		Timer stateCheckTimer = new Timer();
        
        /// <summary>
        /// The current state of this program
        /// </summary>
		private enum RecorderState
        {
            /// <summary>
            /// We are not monitoring for songs
            /// </summary>
            NotRecording = 1,
            /// <summary>
            /// We are monitoring for songs
            /// </summary>
            WaitingForRecording = 2,
            /// <summary>
            /// A song is currently being recorded
            /// </summary>
            Recording = 3,
            /// <summary>
            /// We are shutting down
            /// </summary>
            Closing = 4,
        }

        private SoundCardRecorder SoundCardRecorder { get; set; }
		//private bool MutedSound = false;
        private FolderBrowserDialog folderDialog;
        private RecorderState _currentApplicationState = RecorderState.NotRecording;

        /// <summary>
        /// The current state of the web player
        /// </summary>
		public enum SpotifyState
		{
			Unknown = 0,
			Paused = 1,
			Playing = 2,
			Ad = 3,
		}

		private Mp3Tag currentTrack = new Mp3Tag("","");
		private Mp3Tag recordingTrack = new Mp3Tag("","");
		private SpotifyState currentSpotifyState = SpotifyState.Unknown;

        public class StateChangedEventArgs : EventArgs
        {
            public Mp3Tag Song { get; set; }
            public Mp3Tag PreviousSong { get; set; }
            public SpotifyState State { get; set; }
            public SpotifyState PreviousState { get; set; }
        }

        public delegate void StateChangedEventHandler(object sender, StateChangedEventArgs e);

        public event StateChangedEventHandler StateChanged;

        public MainForm()
        {
            InitializeComponent();

            //check if it is windows 7
            if (Environment.OSVersion.Version.Major < 6)
            {
                MessageBox.Show("This application is optimized for windows 7 or higher");
                Close();
            }

            Load += OnLoad;
            Closing += OnClosing;
            StateChanged += new StateChangedEventHandler(Spotify_StateChanged);

            string baseDir = System.IO.Path.GetDirectoryName( System.Reflection.Assembly.GetEntryAssembly().Location );

            // Initailize the chromium browser
            var settings = new CefSettings();
            //settings.CefCommandLineArgs.Add("enable-system-flash", "1");
            settings.CachePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\DeezerRecorder\\";
            Cef.Initialize(settings);

   //         Xpcom.Initialize(  baseDir + "\\xulrunner" );
			//browser = new GeckoWebBrowser { Dock = DockStyle.Fill };
			//GeckoPreferences.User["general.useragent.override"] = Util.GetDefaultUserAgent();
			//GeckoPreferences.Default["extensions.blocklist.enabled"] = false;   // enables flash. If it does not work, also make sure that "Project -> Properties -> Debug -> Enable Visual Studio hostng process" is not enablable

   //         this.splitContainer1.Panel2.Controls.Add(browser);
            //browser.DocumentTitleChanged += new EventHandler( browser_DocumentTitleChanged );


            ChromiumWebBrowser aboutBrowser = new ChromiumWebBrowser("file://" + baseDir.Replace("\\", "/") + "/about.html");
            tabPageAbout.Controls.Add(aboutBrowser);

            ChromiumWebBrowser helpBrowser = new ChromiumWebBrowser("file://"+baseDir.Replace("\\","/")+"/help.html");
            tabPageHelp.Controls.Add(helpBrowser);

			stateCheckTimer.Interval = 25;
			stateCheckTimer.Tick += new EventHandler( stateCheckTimer_Tick );
			//stateCheckTimer.Start();

			addToLog( "Application started..." );

#if !DEBUG
			tabControl1.TabPages.RemoveByKey("tabPageLog");
#else
			//browser.Navigate( Util.GetDefaultURL() );
#endif
		}

        private void ChangeApplicationState(RecorderState newState)
        {
            ChangeGui(newState);
            addToLog("Now " + newState.ToString());

            switch (_currentApplicationState)
            {
                case RecorderState.NotRecording:
                    switch (newState)
                    {
                        case RecorderState.NotRecording:
                            break;
                        case RecorderState.WaitingForRecording:
                            break;
                        case RecorderState.Recording:
							StartRecording( (MMDevice)deviceListBox.SelectedItem);
                            break;
                        case RecorderState.Closing:
                            break;
                    }

                    break;
                case RecorderState.WaitingForRecording:
                    switch (newState)
                    {
                        case RecorderState.NotRecording:
                            break;
                        case RecorderState.WaitingForRecording:
                            throw new Exception(string.Format("NY {0} - {1}",_currentApplicationState,newState));
                        case RecorderState.Recording:
							StartRecording( (MMDevice)deviceListBox.SelectedItem);
                            break;
                        case RecorderState.Closing:
                            //Close();
                            break;
                    }
                    break;
                case RecorderState.Recording:
                    switch (newState)
                    {
                        case RecorderState.NotRecording:
                            StopRecording();
                            break;
                        case RecorderState.Recording: //file changed
                            StopRecording();
							StartRecording( (MMDevice)deviceListBox.SelectedItem);
                            break;
                        case RecorderState.WaitingForRecording: //file changed
                            StopRecording();
                            break;
                    }
                    break;

            }
            _currentApplicationState = newState;
        }

        private void ChangeGui(RecorderState state)
        {
            switch (state)
            {
                case RecorderState.NotRecording:
                    browseButton.Enabled = true;
                    buttonStartRecording.Enabled = true;
                    buttonStopRecording.Enabled = false;
                    deviceListBox.Enabled = true;
                    break;
                case RecorderState.WaitingForRecording:
                    browseButton.Enabled = false;
                    buttonStartRecording.Enabled = false;
                    buttonStopRecording.Enabled = true;
                    deviceListBox.Enabled = false;
                    break;
                case RecorderState.Recording:
                    browseButton.Enabled = false;
                    buttonStartRecording.Enabled = false;
                    buttonStopRecording.Enabled = true;
                    deviceListBox.Enabled = false;
                    break;
            }
        }

        private void OnLoad(object sender, EventArgs eventArgs)
        {

            // Load the main browser
            mainBrowser = new ChromiumWebBrowser("https://www.deezer.com/")
            {
                BrowserSettings = new BrowserSettings()
                {
                    Plugins = CefState.Enabled
                }
            };
            this.splitContainer1.Panel2.Controls.Add(mainBrowser);

            // Load the timer only when loading is finished
            mainBrowser.LoadingStateChanged += OnLoadingStateChanged;
            
            //load the available devices
            LoadWasapiDevicesCombo();

            //load the different bitrates
            LoadBitrateCombo();

            //Load user settings
            LoadUserSettings();

            //set the change event if filePath is 
            songLabel.Text = string.Empty;
			encodingLabel.Text = string.Empty;

            folderDialog = new FolderBrowserDialog { SelectedPath = outputFolderTextBox.Text };

            versionLabel.Text = string.Format("Version {0}", Application.ProductVersion);

            ChangeApplicationState(_currentApplicationState);

			// instantiate the sound recorder once in an attempt to reduce lag the first time used
			try
			{
				SoundCardRecorder = new SoundCardRecorder( (MMDevice)deviceListBox.SelectedItem, CreateOutputFile( "deleteme", "wav" ), "" );
				SoundCardRecorder.Dispose();
				SoundCardRecorder = null;
				if( File.Exists( CreateOutputFile( "deleteme", "wav" )  ) ) File.Delete( CreateOutputFile( "deleteme", "wav" ) );
			}
			catch( Exception ex )
			{
                addToLog("Error: " + ex.Message);
			}

        }

        private void OnClosing(object sender, CancelEventArgs cancelEventArgs)
        {
			StopRecording();
            ChangeApplicationState(RecorderState.Closing);

            Cef.Shutdown();

			Util.SetDefaultBitrate( bitrateComboBox.SelectedIndex );
            Util.SetDefaultDevice(deviceListBox.SelectedItem.ToString());
            Util.SetDefaultOutputPath(outputFolderTextBox.Text);
            Util.SetDefaultThreshold((int)thresholdTextBox.Value);
            Util.SetDefaultThreshold((int)thresholdTextBox.Value);
            Util.SetDefaultThresholdEnabled(thresholdCheckBox.Checked);
			Util.SetDefaultMuteAdsEnabled( MuteOnAdsCheckBox.Checked );
        }

        private void OnLoadingStateChanged(object sender, LoadingStateChangedEventArgs args)
        {
            if (!args.IsLoading)
                this.Invoke((Action) (() => stateCheckTimer.Start()));
        }

        private void ButtonPlayClick(object sender, EventArgs e)
        {
            if (listBoxRecordings.SelectedItem != null)
            {
                Process.Start(CreateOutputFile((string)listBoxRecordings.SelectedItem, "mp3"));
            }
        }

        private void ButtonDeleteClick(object sender, EventArgs e)
        {
            if (listBoxRecordings.SelectedItem != null)
            {
                try
                {
                    File.Delete(CreateOutputFile((string)listBoxRecordings.SelectedItem, "mp3"));
                    listBoxRecordings.Items.Remove(listBoxRecordings.SelectedItem);
                    if (listBoxRecordings.Items.Count > 0)
                    {
                        listBoxRecordings.SelectedIndex = 0;
                    }
                }
                catch (Exception)
                {
                    MessageBox.Show("Could not delete recording");
                }
            }
        }

        private void ButtonOpenFolderClick(object sender, EventArgs e)
        {
            Process.Start(outputFolderTextBox.Text);
        }

        private void ButtonStartRecordingClick(object sender, EventArgs e)
        {
            ChangeApplicationState(songLabel.Text.Trim().Length > 0
                                       ? RecorderState.Recording
                                       : RecorderState.WaitingForRecording);
        }

        private void ButtonStopRecordingClick(object sender, EventArgs e)
        {
            ChangeApplicationState(RecorderState.NotRecording);
        }

        private void ClearButtonClick(object sender, EventArgs e)
        {
            listBoxRecordings.Items.Clear();
        }

        private string CreateOutputFile(string song, string extension)
        {
            song = RemoveInvalidFilePathCharacters(song, string.Empty);
            return Path.Combine(outputFolderTextBox.Text, string.Format("{0}.{1}", song, extension));
        }

        private void StartRecording(MMDevice device)
        {
            if (device != null)
            {
                if(SoundCardRecorder!=null)
                    StopRecording();

				recordingTrack = new Mp3Tag( currentTrack.Title, currentTrack.Artist );

                string song = recordingTrack.Artist + " - " + recordingTrack.Title;
                string file = CreateOutputFile(song, "wav");

                SoundCardRecorder = new SoundCardRecorder(
								device, file ,
								song );
                SoundCardRecorder.Start();

				addToLog( "Recording!" );
            }
        }

        private void StopRecording()
        {
            string filePath = string.Empty;
            string song = string.Empty;
			int duration = 0;
            if (SoundCardRecorder != null)
            {
				addToLog( "Recording stopped" );

                SoundCardRecorder.Stop();
                filePath = SoundCardRecorder.FilePath;
                song = SoundCardRecorder.Song;
                duration = SoundCardRecorder.Duration;
					addToLog( "Duration: " + duration + " (Limit: " + thresholdTextBox.Value + ")");
				SoundCardRecorder.Dispose();
                SoundCardRecorder = null;

				if( duration < (int)thresholdTextBox.Value && thresholdCheckBox.Checked )
				{
					File.Delete( filePath );
					addToLog( "Recording too short; deleting file..." );
				}
				else
				{
					if( !string.IsNullOrEmpty( filePath ) )
					{
						addToLog( "Recorded file: " + filePath );
						encodingLabel.Text = song;
						PostProcessing( song );
					}
				}
			}
        }

		private void PostProcessing( string song )
		{
			string bitrate = (string)bitrateComboBox.SelectedValue;
			Task t = new Task( () => ConvertToMp3( song, bitrate ) );
			t.Start();
		}

		private void ConvertToMp3( string filePath, string bitrate )
        {
            string wavFile = CreateOutputFile(filePath, "wav");
            if (!File.Exists( wavFile ))
                return;

			addToLog( "Converting to mp3... " );

            Process process = new Process();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

            //Mp3Tag tag = Util.ExtractMp3Tag(filePath);

            process.StartInfo.FileName = "lame.exe";
			process.StartInfo.Arguments = string.Format( "{2} --tt \"{3}\" --ta \"{4}\" --tc \"{5}\"  \"{0}\" \"{1}\"",
				wavFile,
				CreateOutputFile( recordingTrack.Artist + " - " + recordingTrack.Title, "mp3" ),
				bitrate,
				recordingTrack.Title,
				recordingTrack.Artist,
				"" );

            process.StartInfo.WorkingDirectory = new FileInfo(Application.ExecutablePath).DirectoryName;
            addToLog( "Starting LAME..." );
			process.Start();
            //process.WaitForExit(20000);
			process.WaitForExit();
			addToLog( "  LAME exit code: " + process.ExitCode );
			if( !process.HasExited )
			{
				addToLog( "Killing LAME process!" );
				process.Kill();
			}
			addToLog( "LAME finished!" );

			addToLog( "Deleting wav file... " );
            try
            {
                File.Delete(wavFile);
            }
            catch (Exception)
            {

                addToLog("Error while deleting wav file");
            }

			addToLog( "Mp3 ready: " + CreateOutputFile( filePath, "mp3" ) );
			AddSongToList( filePath );
        }

		private void AddSongToList(string song)
		{
			if( this.InvokeRequired )
			{
				// if required for thread safety, call self using invoke instead
				this.Invoke( new MethodInvoker( delegate() { AddSongToList( song ); } ) );
			}
			else
			{
				int newItemIndex = listBoxRecordings.Items.Add( song );
				listBoxRecordings.SelectedIndex = newItemIndex;
				encodingLabel.Text = "";
			}
		}

        private void LoadWasapiDevicesCombo()
        {
            var deviceEnum = new MMDeviceEnumerator();
            var devices = deviceEnum.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();

            deviceListBox.DataSource = devices;
            deviceListBox.DisplayMember = "FriendlyName";
        }
        private void LoadBitrateCombo()
        {
			Dictionary<string, string> bitrate = new Dictionary<string, string>();
			bitrate.Add( "VBR Extreme (V0)" , "--preset extreme" );
			bitrate.Add( "VBR Standard (V2)" , "--preset standard" );
			bitrate.Add( "VBR Medium (V5)" , "--preset medium" );
			bitrate.Add( "CBR 320" , "--preset insane" );
			bitrate.Add( "CBR 256" , "-b 256" );
			bitrate.Add( "CBR 192" , "-b 192" );
			bitrate.Add( "CBR 160" , "-b 160" );
			bitrate.Add( "CBR 128" , "-b 128" );
			bitrate.Add( "CBR 96" , "-b 96" );

			bitrateComboBox.DataSource = new BindingSource( bitrate	, null ); ;
			bitrateComboBox.DisplayMember = "Key";
			bitrateComboBox.ValueMember = "Value";
        }

        /// <summary>
        /// load the setting from a previous session
        /// </summary>
        private void LoadUserSettings()
        {
            //get/set the device
            string defaultDevice = Util.GetDefaultDevice();

            foreach (MMDevice device in deviceListBox.Items)
            {
                if (device.FriendlyName.Equals(defaultDevice))
                    deviceListBox.SelectedItem = device;
            }

            //set the default output to the music directory
            outputFolderTextBox.Text = Util.GetDefaultOutputPath();

            //set the default bitrate
            bitrateComboBox.SelectedIndex = Util.GetDefaultBitrate();

            thresholdTextBox.Value = Util.GetDefaultThreshold();
            thresholdCheckBox.Checked = Util.GetDefaultThresholdEnabled();
			MuteOnAdsCheckBox.Checked = Util.GetDefaultMuteAdsEnabled();

        }

        public static string RemoveInvalidFilePathCharacters(string filename, string replaceChar)
        {
            string regexSearch = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            Regex r = new Regex(string.Format("[{0}]", Regex.Escape(regexSearch)));
            return r.Replace(filename, replaceChar);
        }

        private void BrowseButtonClick(object sender, EventArgs e)
        {
            if (folderDialog.ShowDialog() == DialogResult.OK)
            {
                outputFolderTextBox.Text = folderDialog.SelectedPath;
                Util.SetDefaultOutputPath(folderDialog.SelectedPath);
            }
        }

		private void webBrowser_DocumentCompleted( object sender, WebBrowserDocumentCompletedEventArgs e )
		{
			//Console.Write("Loaded!");
		}
		private void webBrowser_Navigating( object sender, WebBrowserNavigatingEventArgs e )
		{
			//Console.WriteLine( "Navigating to: " + e.Url );
		}

		private void toolStripButton_Home_Click( object sender, EventArgs e )
		{
            mainBrowser.Load("https://www.deezer.com/");
		}

		private void toolStripButton_Back_Click( object sender, EventArgs e )
		{
            mainBrowser.Back();
		}

		private void addToLog( string text )
		{
			if( this.InvokeRequired )
			{
				// if required for thread safety, call self using invoke instead
				this.Invoke( new MethodInvoker( delegate() { addToLog( text ); } ) );
			}
			else
			{
				System.Diagnostics.Debug.WriteLine( "[" + DateTime.Now.ToShortTimeString() + "] " + text );
				listBoxLog.Items.Add( "[" + DateTime.Now.ToShortTimeString() + "] " + text );
				listBoxLog.SelectedIndex = listBoxLog.Items.Count-1;
			}
		}

		private void thresholdCheckBox_CheckedChanged( object sender, EventArgs e )
		{
			thresholdTextBox.Enabled = thresholdCheckBox.Checked;
		}

		private void toolStripMenuItem_Play_Click( object sender, EventArgs e )
		{
			if( listBoxRecordings.SelectedItem != null )
			{
				try
				{
					Process.Start( CreateOutputFile( (string)listBoxRecordings.SelectedItem, "mp3" ) );
				}
				catch
				{
					MessageBox.Show( "Could not play song..." );
				}
			}
		}

		private void toolStripMenuItem_Open_Click( object sender, EventArgs e )
		{
			Process.Start( outputFolderTextBox.Text );
		}

		private void toolStripMenuItem_Delete_Click( object sender, EventArgs e )
		{
			if( listBoxRecordings.SelectedItem != null )
			{
				try
				{
					File.Delete( CreateOutputFile( (string)listBoxRecordings.SelectedItem, "mp3" ) );
					listBoxRecordings.Items.Remove( listBoxRecordings.SelectedItem );
					if( listBoxRecordings.Items.Count > 0 )
					{
						listBoxRecordings.SelectedIndex = 0;
					}
				}
				catch( Exception )
				{
					MessageBox.Show( "Could not delete recording..." );
				}
			}
		}

		private void toolStripMenuItem_ClearList_Click( object sender, EventArgs e )
		{
			listBoxRecordings.Items.Clear();
		}

		private void openRecordingDevicesButton_Click( object sender, EventArgs e )
		{
			Process.Start( "control.exe" , "mmsys.cpl,,1");

			/*
			 * If you want to access the Mixer and the other functions, you can use these shortcuts:
			• Master Volume Left: SndVol.exe -f 0
			• Master Volume Right: SndVol.exe -f 49825268
			• Volume Mixer Left: SndVol.exe -r 0
			• Volume Mixer Right: SndVol.exe -r 49490633
			• Playback Devices: control.exe mmsys.cpl,,0
			• Recording Devices: control.exe mmsys.cpl,,1
			• Sounds: control.exe mmsys.cpl,,2
			From http://www.errorforum.com/microsoft-windows-vista-error/4636-vista-tips-tricks-tweaks.html
			 * */

		}

		private void OpenMixerButtonClick( object sender, EventArgs e )
		{
			Process.Start( "sndvol" );
		}

        public void Spotify_StateChanged( object sender, StateChangedEventArgs e )
        {
            addToLog("Change detected");
            currentSpotifyState = e.State;
            currentTrack = e.Song;
            if (e.State == SpotifyState.Playing)
            {
                string song = e.Song.ToString();
                songLabel.Text = song;
                addToLog("Now playing: " + song);
                // If we are monitoring, set the state to recording
                if (_currentApplicationState == RecorderState.WaitingForRecording)
                {
                    ChangeApplicationState(RecorderState.Recording);
                }
                // If we are already recording, stop the recording and restart it. 
                else if (_currentApplicationState == RecorderState.Recording)
                {
                    ChangeApplicationState(RecorderState.WaitingForRecording);
                    ChangeApplicationState(RecorderState.Recording);
                }
            }
            else if (e.State == SpotifyState.Paused)
            {
                addToLog("Music paused or stopped");
                // If we were recording a song, now we aren't anymore
                if (_currentApplicationState == RecorderState.Recording)
                {
                    ChangeApplicationState(RecorderState.WaitingForRecording);
                }
            }
        }


		void stateCheckTimer_Tick( object sender, EventArgs e )
		{
            SpotifyState oldState = currentSpotifyState;
            Mp3Tag oldTrack = new Mp3Tag(currentTrack.Title, currentTrack.Artist);

            // figure out what Deezer is doing now
            // therefore, we need to execute a small javascript code
            // which returns all HTML elements with a specific tag
            // the first element of these gives us the song title
            // the next elements give us the artists

            // Get this array
            string script = "[dzPlayer.getSongTitle(), dzPlayer.getArtistName()]";

            try
            {
                mainBrowser.EvaluateScriptAsync(script).ContinueWith(x =>
                {
                    var response = x.Result;

                    if (response.Success && response.Result != null)
                    {
                        var list = (List<object>)response.Result;

                        string artist = list[1].ToString();
                        string title = list[0].ToString();

                        //currentTrack = new Mp3Tag(title, artist);
                        this.Invoke((Action)(() =>
                        {
                            //currentTrack = new Mp3Tag(title, artist);
                            Mp3Tag newTag = new Mp3Tag(title, artist);
                            if ( !(newTag.Equals(currentTrack)) )
                            {
                                StateChanged(this, new StateChangedEventArgs()
                                {
                                    Song = newTag,
                                    PreviousSong = currentTrack,
                                    State = currentSpotifyState,
                                    PreviousState = currentSpotifyState
                                });
                            }
                        }));
                    }
                });
            }
            catch(Exception ex)
            {
                addToLog("Error: " + ex.Message);
            }


            // Find out if Deezer is paused or playing
            string scriptIsPlaying = "dzPlayer.isPlaying()";

            try
            {
                mainBrowser.EvaluateScriptAsync(scriptIsPlaying).ContinueWith(y =>
                {
                    var responseIsPlaying = y.Result;

                    if (responseIsPlaying.Success && responseIsPlaying.Result != null)
                    {
                        bool isPlaying = (bool)responseIsPlaying.Result;
                        if (isPlaying)
                        {
                            this.Invoke((Action)(() =>
                            {
                                if ( currentSpotifyState != SpotifyState.Playing )
                                {
                                    StateChanged(this, new StateChangedEventArgs()
                                    {
                                        PreviousState = currentSpotifyState,
                                        State = SpotifyState.Playing,
                                        Song = currentTrack,
                                        PreviousSong = currentTrack
                                    });
                                }
                            }));
                        }
                        else
                        {
                            this.Invoke((Action)(() =>
                            {
                                if (currentSpotifyState != SpotifyState.Paused)
                                {
                                    StateChanged(this, new StateChangedEventArgs()
                                    {
                                        PreviousState = currentSpotifyState,
                                        State = SpotifyState.Paused,
                                        Song = currentTrack,
                                        PreviousSong = currentTrack
                                    });
                                }
                            }));
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                addToLog("Error: " + ex.Message);
            }

            //// Check if state is different
            //// and handle changes accordingly
            //if ( oldState != currentSpotifyState || !(oldTrack.Equals(currentTrack) ))
            //{
            //    addToLog("Change detected");
            //    if ( currentSpotifyState == SpotifyState.Playing )
            //    {
            //        string song = currentTrack.ToString();
            //        songLabel.Text = song;
            //        addToLog("Now playing: " + song);
            //        // If we are not not monitoring, set the state to recording
            //        if ( _currentApplicationState != RecorderState.NotRecording &&
            //            !(oldTrack.Equals(currentTrack)))
            //        {
            //            ChangeApplicationState(RecorderState.Recording);
            //        }
            //        else if ( _currentApplicationState == RecorderState.Recording )
            //        {
            //            ChangeApplicationState(RecorderState.WaitingForRecording);
            //        }
            //    }
            //    else if ( currentSpotifyState == SpotifyState.Paused )
            //    {
            //        addToLog("Music paused or stopped");
            //        if ( _currentApplicationState == RecorderState.Recording )
            //        {
            //            ChangeApplicationState(RecorderState.WaitingForRecording);
            //        }
            //    }
            //}
        }

	}
}

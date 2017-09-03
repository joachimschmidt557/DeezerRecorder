﻿using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

namespace SpotifyWebRecorder.Forms.UI
{
    public class Util
    {
		private const int APPCOMMAND_VOLUME_MUTE = 0x80000;
		private const int WM_APPCOMMAND = 0x319;
		[DllImport( "user32.dll" )]
		public static extern IntPtr SendMessageW( IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam );
		public static void ToggleMuteVolume(IntPtr Handle)	// call from a form and set handle to "this"
		{
			SendMessageW(Handle, WM_APPCOMMAND, Handle, (IntPtr) APPCOMMAND_VOLUME_MUTE);
		}

        public static string GetDefaultOutputPath()
        {
            if (string.IsNullOrEmpty(Settings.Default.OutputPath))
            {
                Settings.Default.OutputPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "DeezerRecorder");
                if (!Directory.Exists(Settings.Default.OutputPath))
                    Directory.CreateDirectory(Settings.Default.OutputPath);
                Settings.Default.Save();
            }

            return Settings.Default.OutputPath;
        }
        public static void SetDefaultOutputPath(string outputPath)
        {
            Settings.Default.OutputPath = outputPath;
            Settings.Default.Save();
        }
        public static string GetDefaultDevice()
        {
            return Settings.Default.DefaultDevice;
            
        }
        public static void SetDefaultDevice(string device)
        {
            Settings.Default.DefaultDevice = device;
            Settings.Default.Save();
        }
        public static int GetDefaultBitrate()
        {
            return Settings.Default.Bitrate;
        }
        public static void SetDefaultThreshold(int threshold)
        {
            Settings.Default.DeleteThreshold = threshold;
            Settings.Default.Save();
        }
        public static int GetDefaultThreshold()
        {
            return Settings.Default.DeleteThreshold;

        }
        public static void SetDefaultThresholdEnabled(bool threshold)
        {
            Settings.Default.DeleteThresholdEnabled = threshold;
            Settings.Default.Save();
        }
        public static bool GetDefaultThresholdEnabled()
        {
            return Settings.Default.DeleteThresholdEnabled;

        }
		public static void SetDefaultMuteAdsEnabled( bool mute )
		{
			Settings.Default.MuteAds  = mute;
			Settings.Default.Save();
		}
		public static bool GetDefaultMuteAdsEnabled()
		{
			return Settings.Default.MuteAds;

		}
		public static void SetDefaultBitrate( int bitrate )
        {
            Settings.Default.Bitrate = bitrate;
            Settings.Default.Save();
        }

	}
}
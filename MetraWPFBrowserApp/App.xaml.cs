﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace MetraWPFBrowserApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Logs any uncaught exceptions.
        /// </summary>
        public void LogExceptionHandler(object s, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs args)
        {
            Exception e = args.Exception;
            LogManager.WriteToLog("Exception " + e.GetType().ToString() + " occured with message: " + e.Message);
            MessageBox.Show("Exception " + e.GetType().ToString() + " occured with message: " + e.Message);
            LogManager.FlushLog();
        }
        /// <summary>
        /// Logs exit event and flushes all unwritten events from the log.
        /// </summary>
        public void LogExitHandler(object s, ExitEventArgs args)
        {
            LogManager.WriteToLog("Application exited.");
            LogManager.FlushLog();
        }
    }
}

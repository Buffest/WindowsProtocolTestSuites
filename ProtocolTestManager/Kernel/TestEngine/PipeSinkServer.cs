﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace Microsoft.Protocols.TestManager.Kernel
{
    // Use to get the output of QTAgent console.
    public class PipeSinkServer
    {
        static string PipeName;
        static List<Listener> listeners;
        static IAsyncResult listenerResult = null;
        static NamedPipeServerStream waitingServer = null;
        public static void serverCallback(IAsyncResult result)
        {
            NamedPipeServerStream server = (NamedPipeServerStream)result.AsyncState;
            server.EndWaitForConnection(result);
            StreamReader reader = new StreamReader(server);
            StreamWriter writer = new StreamWriter(server);
            Listener listener = new Listener(reader, server);
            listeners.Add(listener);
            waitingServer = new NamedPipeServerStream(PipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Message, PipeOptions.Asynchronous);
            listenerResult = waitingServer.BeginWaitForConnection(new AsyncCallback(serverCallback), waitingServer);
        }

        /// <summary>
        /// Start PipeSink of PTF.
        /// </summary>
        public static void Start(string pipeName)
        {
            listeners = new List<Listener>();
            PipeName = pipeName;
            if (waitingServer == null)
            {
                waitingServer = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 10, PipeTransmissionMode.Message, PipeOptions.Asynchronous);
                listenerResult = waitingServer.BeginWaitForConnection(new AsyncCallback(serverCallback), waitingServer);
            }
            Listener.IgnoreLogs = false;
        }

        /// <summary>
        /// Stop PipeSink
        /// </summary>
        public static void Stop()
        {
            Listener.IgnoreLogs = true;
            foreach (var listener in listeners)
            {
                listener.Stop();
            }
        }

        public delegate void ParseLogMessageCallback(string message);
        public static ParseLogMessageCallback ParseLogMessage;
    
    
    }

    /// <summary>
    /// Listener of NamedPipe.
    /// </summary>
    public class Listener
    {
        public static bool IgnoreLogs;
        StreamReader SR;
        NamedPipeServerStream serverStream;
        Thread thread;
        public Listener(StreamReader reader, NamedPipeServerStream server)
        {
            serverStream = server;
            SR = reader;
            thread = new Thread(new ThreadStart(Run));
            thread.Start();
        }
        /// <summary>
        /// Stop to get the stream.
        /// </summary>
        public void Stop()
        {
            if (thread != null && thread.ThreadState == System.Threading.ThreadState.Running)
            {
                thread.Abort();
            }
            SR.Close();
            serverStream.Close();
        }
        /// <summary>
        /// Start to get the output.
        /// </summary>
        public void Run()
        {

            string line;
            while ((line = SR.ReadLine()) != null)
            {
                if (!IgnoreLogs && PipeSinkServer.ParseLogMessage != null)
                    PipeSinkServer.ParseLogMessage(line);
            }
        }
    }
}
﻿using System;
using System.Threading;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace Leayal.ApplicationController
{
    /// <summary>
    /// Base class provides skeleton for application controlling. If it's in single-instance model, the class will use <see cref="MemoryMappedFile"/> to pass the command-line from subsequent instances to the first instance.
    /// </summary>
    /// <remarks>
    /// The model run in this order: <see cref="Run(string[])"/>-><see cref="OnRun(string[])"/>-><see cref="OnStartup(StartupEventArgs)"/>-><see cref="OnStartupNextInstance(StartupNextInstanceEventArgs)"/>
    /// Do not mistake with Visual Basic's execute order.
    /// </remarks>
    public abstract class ApplicationBase : Microsoft.VisualBasic.ApplicationServices.ApplicationBase
    {
        private bool _isRunning;
        private string _instanceID;

        /// <summary>
        /// Get a boolean determine whether this instance is in single-instance model
        /// </summary>
        protected bool IsSingleInstance => !string.IsNullOrEmpty(this._instanceID);

        /// <summary>
        /// Get the unique instance ID of this application instance
        /// </summary>
        public string SingleInstanceID => this._instanceID;

        /// <summary>
        /// Set the unique instance ID for this application instance. Must be called before <see cref="Run(string[])"/>.
        /// </summary>
        /// <param name="instanceID">The unique ID. Set to null to disable single-instance model</param>
        protected void SetSingleInstanceID(string instanceID)
        {
            this.EnsureApplicationIsNotRunning();
            this._instanceID = instanceID;
        }

        private string GetArgsWriterID(Guid guid)
        {
            return this._instanceID + "-arg-reader" + guid.ToString();
        }


#if NETSTANDARD2_0
        /// <summary>Initializes a new instance of the <see cref="ApplicationBase"/> class with <see cref="ApplicationBase.IsSingleInstance"/> is false</summary>
        /// <exception cref="InvalidOperationException">Cannot generate unique ID from assembly's GUID. Mainly because the GUID cannot be found.</exception>
        [Obsolete("You should not use this. Auto-generate unique ID from assembly's information is not working well and may throw exception.", false)]
        protected ApplicationBase() : this(false) { }
#else
        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationBase"/> class with <see cref="ApplicationBase.IsSingleInstance"/> is false.
        /// </summary>
        protected ApplicationBase() : this(false) { }
#endif

#if NETSTANDARD2_0
        /// <summary>Initializes a new instance of the <see cref="ApplicationBase"/> class</summary>
        /// <param name="isSingleInstance">Determine whether the application is in single-instance model.</param>
        /// <exception cref="InvalidOperationException">Happens when <paramref name="isSingleInstance"/> is true and the application cannot generate unique ID from the calling assembly</exception>
        protected ApplicationBase(bool isSingleInstance) : this(isSingleInstance, null) { }
#else
        /// <summary>Initializes a new instance of the <see cref="ApplicationBase"/> class</summary>
        protected ApplicationBase(bool isSingleInstance) : this(isSingleInstance, null) { }
#endif

        /// <summary>Initializes a new instance of the <see cref="ApplicationBase"/> class in single-instance model with given instanceID</summary>
        /// <param name="instanceID">The unique ID for the application to check for</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="instanceID"/> is longer than 200 characters</exception>
        protected ApplicationBase(string instanceID) : this(true, instanceID) { }

        /// <summary>Nope. Don't even use System.Reflection to invoke this method, please.</summary>
        /// <param name="isSingleInstance"></param>
        /// <param name="instanceID"></param>
        private ApplicationBase(bool isSingleInstance, string instanceID)
        {
            if (isSingleInstance)
            {
                if (string.IsNullOrEmpty(instanceID))
                {
                    instanceID = this.GetApplicationID();
                }
                else if (instanceID.Length > 200)
                {
                    throw new ArgumentOutOfRangeException("The given instance ID is longer than 200 characters");
                }
                this.SetSingleInstanceID(instanceID);
            }
            else
            {
                this.SetSingleInstanceID(null);
            }
        }

        /// <summary>Sets up and starts the application model with command-line arguments from <see cref="Environment.GetCommandLineArgs"/></summary>
        public void Run()
        {
            string[] tmp = Environment.GetCommandLineArgs(),
                args = new string[tmp.Length - 1];
            Buffer.BlockCopy(tmp, 1, args, 0, args.Length);
            // Array.Copy(tmp, 1, args, 0, args.Length);
            tmp = null;
            this.OnRun(args);
            args = null;
        }

        /// <summary>Sets up and starts the application model with given command-line arguments</summary>
        /// <param name="args">The command-line arguments</param>
        public void Run(string[] args)
        {
            this.OnRun(args);
        }

        /// <summary>
        /// When overridden in a derived class, allows for code to run when the application starts.
        /// </summary>
        /// <param name="eventArgs">Contains the command-line arguments of the application</param>
        protected virtual void OnStartup(StartupEventArgs eventArgs)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// When overridden in a derived class, allows for code to run when a subsequent instance of a single-instance application starts. This method will never be invoked if the application is not in single-instance model.
        /// </summary>
        /// <param name="eventArgs">Contains the command-line arguments of the subsequent application instance</param>
        protected virtual void OnStartupNextInstance(StartupNextInstanceEventArgs eventArgs)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Provides the starting point for when the main application is ready to start running, after the initialization is done.
        /// </summary>
        /// <param name="args"></param>
        private void OnRun(string[] args)
        {
            if (this._isRunning) return;
            this._isRunning = true;

            if (this.IsSingleInstance)
            {
                this.CreateSingleInstanceModel(args);
            }
            else
            {
                this.OnStartup(new StartupEventArgs(args));
            }
        }

        private void CreateSingleInstanceModel(string[] args)
        {
            SubsequentProcessPacket packetHolder = new SubsequentProcessPacket();
            byte[] dummypacket = packetHolder.BuildPacket();
            using (Mutex mutex = new Mutex(true, Path.Combine("Global", this._instanceID), out var isNewInstanceJustForMeHuh))
            using (EventWaitHandle waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, this._instanceID))
            {
                RegisteredWaitHandle waiter = null;
                try
                {
                    if (isNewInstanceJustForMeHuh)
                    {
                        using (MemoryMappedFile mmf = MemoryMappedFile.CreateNew(this._instanceID + "-args", dummypacket.Length, MemoryMappedFileAccess.ReadWrite))
                        {
                            waiter = ThreadPool.RegisterWaitForSingleObject(waitHandle, new WaitOrTimerCallback((sender, signal) =>
                            {
                                using (var stream = mmf.CreateViewStream(0, dummypacket.Length))
                                {
                                    var packet = SubsequentProcessPacket.FromStream(stream);
                                    string[] theArgs = new string[packet.ArgumentCount];

                                    string readerIDstring = packet.SharedID.ToString();
                                    if (EventWaitHandle_TryOpenExisting(this._instanceID + "-argreader" + readerIDstring, out var argWaiter))
                                    {
                                        try
                                        {
                                            if (packet.ArgumentCount != 0)
                                            {
                                                using (MemoryMappedFile argdata = MemoryMappedFile.OpenExisting(this._instanceID + "-argdata" + readerIDstring, MemoryMappedFileRights.Read))
                                                using (BinaryReader br = new BinaryReader(argdata.CreateViewStream(0, packet.DataLength, MemoryMappedFileAccess.Read)))
                                                {
                                                    int biggestbuffer = br.ReadInt32();
                                                    byte[] buffer = new byte[biggestbuffer];

                                                    for (int i = 0; i < theArgs.Length; i++)
                                                    {
                                                        int bufferlength = br.ReadInt32();
                                                        br.Read(buffer, 0, bufferlength);
                                                        theArgs[i] = System.Text.Encoding.Unicode.GetString(buffer, 0, bufferlength);
                                                    }
                                                }
                                            }

                                            this.OnStartupNextInstance(new StartupNextInstanceEventArgs(theArgs));
                                        }
                                        catch (Exception ex)
                                        {
                                            if (System.Diagnostics.Debugger.IsAttached)
                                            {
                                                throw;
                                            }
                                            else
                                            {
                                                this.OnStartupNextInstance(new StartupNextInstanceEventArgs(ex));
                                            }
                                        }
                                        finally
                                        {
                                            argWaiter.Set();
                                            argWaiter.Dispose();
                                        }
                                    }
                                }
                                // this._instanceID + "-argreader" + readerID
                                // this.OnStartupNextInstance(e);
                            }), null, -1, false);
                            this.OnStartup(new StartupEventArgs(args));
                        }
                    }
                    else
                    {
                        Guid readerID = Guid.NewGuid();
                        using (Mutex writerMutex = new Mutex(true, Path.Combine("Global", this._instanceID + "-argwriter"), out var isNewWriterMutex))
                        {
                            if (!isNewWriterMutex)
                            {
                                while (!writerMutex.WaitOne()) { }
                            }
                            using (MemoryMappedFile mmf = MemoryMappedFile.OpenExisting(this._instanceID + "-args", MemoryMappedFileRights.Write))
                            using (var accessor = mmf.CreateViewStream(0, dummypacket.Length, MemoryMappedFileAccess.Write))
                            {
                                try
                                {
                                    if (args.Length == 0)
                                    {
                                        using (EventWaitHandle argWaiter = new EventWaitHandle(false, EventResetMode.ManualReset, this._instanceID + "-argreader" + readerID.ToString(), out var newInstanceForArgs))
                                        {
                                            if (newInstanceForArgs)
                                            {
                                                packetHolder.ArgumentCount = args.Length;
                                                packetHolder.IsMemorySharing = true;
                                                packetHolder.SharedID = readerID;
                                                packetHolder.DataLength = 0;

                                                int size = packetHolder.BuildPacket(dummypacket, 0);

                                                accessor.Write(dummypacket, 0, size);
                                                waitHandle.Set();
                                                // wait until the reader finished get the args
                                                // May cause deadlock if the main instance fail to call Set(). So a timeout is required.
                                                // 10-second is overkill but it's safe???
                                                argWaiter.WaitOne(TimeSpan.FromSeconds(10));
                                            }
                                            else
                                            {
                                                accessor.Write(dummypacket, 0, dummypacket.Length);
                                                waitHandle.Set();
                                            }
                                        }
                                    }
                                    else
                                    {
                                        int sizeofInt = sizeof(int);
                                        // Ensure that the buffer will never be below 4-byte integer so that we can use the buffer for the int->byte conversion.
                                        int biggestSize = 0;
                                        long datasize = (args.Length * sizeofInt) + sizeofInt;
                                        for (int i = 0; i < args.Length; i++)
                                        {
                                            int aaaaaa = System.Text.Encoding.Unicode.GetByteCount(args[i]);
                                            if (biggestSize < aaaaaa)
                                                biggestSize = aaaaaa;
                                            datasize += aaaaaa;
                                        }

                                        biggestSize += sizeofInt;
                                        if (biggestSize < dummypacket.Length)
                                            biggestSize = dummypacket.Length;

                                        packetHolder.ArgumentCount = args.Length;
                                        packetHolder.IsMemorySharing = true;
                                        packetHolder.SharedID = readerID;
                                        packetHolder.DataLength = datasize;

                                        using (MemoryMappedFile argData = MemoryMappedFile.CreateNew(this._instanceID + "-argdata" + readerID.ToString(), datasize, MemoryMappedFileAccess.ReadWrite))
                                        using (EventWaitHandle argWaiter = new EventWaitHandle(false, EventResetMode.ManualReset, this._instanceID + "-argreader" + readerID.ToString(), out var newInstanceForArgs))
                                        {
                                            if (newInstanceForArgs)
                                            {
                                                byte[] buffer = new byte[biggestSize];
                                                using (var datastream = argData.CreateViewStream(0, datasize, MemoryMappedFileAccess.Write))
                                                {
                                                    GetBytesFromIntToBuffer(biggestSize, buffer, 0);

                                                    datastream.Write(buffer, 0, sizeofInt);

                                                    for (int i = 0; i < args.Length; i++)
                                                    {
                                                        int encodedbytelength = System.Text.Encoding.Unicode.GetBytes(args[i], 0, args[i].Length, buffer, sizeofInt);
                                                        GetBytesFromIntToBuffer(encodedbytelength, buffer, 0);

                                                        datastream.Write(buffer, 0, encodedbytelength + sizeofInt);
                                                    }

                                                    datastream.Flush();
                                                }

                                                int packetsize = packetHolder.BuildPacket(buffer, 0);
                                                accessor.Write(buffer, 0, packetsize);
                                                buffer = null;
                                                waitHandle.Set();
                                                // wait until the reader finished get the args
                                                // May cause deadlock if the main instance fail to call Set(). So a timeout is required.
                                                // 10-second is overkill but it's safe???
                                                argWaiter.WaitOne(TimeSpan.FromSeconds(10));
                                            }
                                            else
                                            {
                                                // 
                                                throw new Exception();
                                            }
                                        }
                                    }
                                }
                                catch
                                {
                                    accessor.Write(dummypacket, 0, dummypacket.Length);
                                    waitHandle.Set();

                                    if (System.Diagnostics.Debugger.IsAttached)
                                    {
                                        throw;
                                    }
                                }
                            }

                            if (isNewWriterMutex)
                            {
                                writerMutex.ReleaseMutex();
                            }
                        }
                    }
                }
                finally
                {
                    if (waiter != null)
                    {
                        waiter.Unregister(null);
                    }

                    // Is this required since the mutext is disposed and the instance is exited anyway?
                    // Better safe than sorry.
                    if (isNewInstanceJustForMeHuh)
                    {
                        mutex.ReleaseMutex();
                    }
                }
            }
        }

        private static bool EventWaitHandle_TryOpenExisting(string name, out EventWaitHandle waithandle) => EventWaitHandle.TryOpenExisting(name, out waithandle);

        private string GetApplicationID()
        {
            var entryAssembly = Assembly.GetEntryAssembly();
            var attribute = entryAssembly.GetCustomAttributes(typeof(GuidAttribute)).FirstOrDefault() as GuidAttribute;
            if (attribute != null)
                return attribute.Value;
            else
                throw new InvalidOperationException("Cannot generate unique ID from assembly GUID");
        }

        private void EnsureApplicationIsNotRunning()
        {
            if (this._isRunning)
                throw new InvalidOperationException("Cannot change property while the application is running.");
        }

        internal static void GetBytesFromIntToBuffer(int value, byte[] buffer, int offset)
        {
            unsafe
            {
                fixed (byte* b = buffer)
                {
                    *((int*)(b + offset)) = value;
                }
            }
        }
    }
}

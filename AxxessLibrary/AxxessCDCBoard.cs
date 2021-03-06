﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;


namespace Metra.Axxess
{
    /// <summary>
    /// Class represents an Axxess board using Microchip CDC virtual serial port technology to communicate.
    /// </summary>
    /// <remarks>
    /// Port Settings: 
    ///     Baud <- 115200 
    ///     Parity <- N 
    ///     Data <- 8 
    ///     Stop <- 1
    ///</remarks>
    public class AxxessCDCBoard : CDCDevice, IAxxessBoard
    {
        public bool TestMode { get; set; }

        //Board attributes
        public virtual int PacketSize { get; protected set; }

        public string ProductID { get; protected set; }
        public string AppFirmwareVersion
        {
            get
            {
                return _appVer.ToString();
            }
            protected set
            {
                _appVer = new AxxessFirmwareVersion(value);
            }
        }
        AxxessFirmwareVersion _appVer;
        public string BootFirmwareVersion
        {
            get
            {
                return _bootVer.ToString();
            }
            protected set
            {
                _bootVer = new AxxessFirmwareVersion(value);
            }
        }
        AxxessFirmwareVersion _bootVer;
        public string Info 
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat("Product ID: {0}, App Ver: {1}, Boot Ver: {2}", this.ProductID, this.AppFirmwareVersion, this.BootFirmwareVersion);
                return sb.ToString();
            }
        }
        public ASWCInfo ASWCInformation { get; set; }
        private BoardStatus Status { get; set; }        

        public byte[] IntroPacket { get; protected set; }
        public byte[] ReadyPacket { get; protected set; }
        public byte[] ASWCRequestPacket { get; protected set; }

        public BoardType Type { get; protected set; }

        internal AxxessCDCBoard() : this(false) { }

        internal AxxessCDCBoard(bool testMode = false)
            : base(115200, Parity.None, 8, StopBits.One, Handshake.None, readTimeOut: 50, writeTimeOut: 500, testMode: testMode)
        {
            this.TestMode = testMode;

            this.Type = BoardType.MicrochipCDC;

            this.ProductID = String.Empty;
            this.AppFirmwareVersion = String.Empty;
            this.BootFirmwareVersion = String.Empty;
            this.ASWCInformation = null;
            this.Status = BoardStatus.Idle;

            this.PacketSize = 44;

            //Intro: 01 F0 10 03 A0 01 0F 58 04
            this.IntroPacket = this.PrepPacket(new byte[] { 0x01, 0xF0, 0x10, 0x03, 0xA0, 0x01, 0x0F, 0x58, 0x04 });
            //Ready: 01 F0 20 00 EB 04
            this.ReadyPacket = this.PrepPacket(new byte[] { 0x01, 0xF0, 0x20, 0x00, 0xEB, 0x04 });
            this.ASWCRequestPacket = this.PrepPacket(new byte[] { 0x01, 0xF0, 0xA0, 0x03, 0x10, 0x01, 0x00, 0x57, 0x04 });

            this.OnIntro += ParseIntroPacket;
        }       

        //Atomic packet operations
        public void SendIntroPacket()
        {
            this.Write(this.IntroPacket);
        }
        public void SendReadyPacket()
        {
            this.Write(this.ReadyPacket);
        }
        public void SendASWCRequestPacket()
        {
            this.Write(this.ASWCRequestPacket);
        }

        /// <summary>
        /// Method to extract board versioning info from intro response packets
        /// </summary>
        /// <param name="packet">The received packet</param>
        /// <returns>True of intro packet, else false</returns>
        protected virtual void ParseIntroPacket(object sender, PacketEventArgs args)
        {
            //Util.TestConsoleWrite(this.TestMode, "Parsing for intro packet!");
            byte[] packet = args.Packet;
            String content = String.Empty;
            foreach (byte b in packet)
            {
                content += Convert.ToChar(b);
            }

            if (content.Substring(6, 3).Equals("CWI"))
            {
                this.ProductID = content.Substring(6, 9);
                this.AppFirmwareVersion = content.Substring(25, 3);

                Log.Write("");
                this.OnIntro -= ParseIntroPacket;
            }
        }
        protected virtual void ProcessIntroPacket(byte[] packet)
        {
            //43, 57, 49 == CWI in ascii
            if (packet.Length > 9
                && packet[6] == 0x43
                && packet[7] == 0x57
                && packet[8] == 0x49)
                this.OnIntroReceived(new PacketEventArgs(packet));
        }

        public virtual byte[] PrepPacket(byte[] packet) { return packet; }

        //Event related stuff
        public virtual bool IsAck(byte[] packet) 
        {
            if (packet[packet.Length - 1] == 0x41)
            {
                Log.Write("Ack packet received!");
                return true;
            }
            else { return false; }
        }
        public virtual bool IsFinal(byte[] packet) 
        {
            return false;
        }
        public virtual bool IsASWCRead(byte[] packet)
        {
            if (packet.Length < 4) return false;
            return packet[0] == 0x01
                && packet[1] == 0x0F
                && packet[2] == 0xA0;
        }
        public virtual bool IsASWCConfirm(byte[] packet)
        {
            return packet[1] == 0x01
                && packet[2] == 0x0F
                && packet[3] == 0xA8
                && packet[4] == 0x01;
        }

        /// <summary>
        /// This method is called asynchronously when a packet is received.
        /// It will forward the packet to the appropriate event handler based on identification
        /// </summary>
        /// <param name="packet">The raw serialized data packet</param>
        protected override void HandleDataReceived(byte[] packet)
        {
            if (OnPacket != null)
                OnPacketReceieved(new PacketEventArgs(packet));
            if (OnIntro != null)
                ProcessIntroPacket(packet);

            if (this.OnAck != null && this.IsAck(packet)) this.OnAckReceived(new PacketEventArgs(packet));
            else if (this.OnFinal != null && this.IsFinal(packet)) this.OnFinalReceived(new PacketEventArgs(packet));
            
            if (this.OnASWCInfo != null && this.IsASWCRead(packet)) this.OnASWCInfoReceieved(new ASWCEventArgs(new ASWCInfo(packet)));
        }

        public InputReport CreateInputReport()
        {
            return new AxxessInputReport(this);
        }

        public virtual byte CalculateChecksum(byte[] packet)
        {
            return 0x00;
        }

        #region Events
        public event IntroEventHandler OnIntro;
        public event AckEventHandler OnAck;
        public event FinalEventHandler OnFinal;
        public event ASWCInfoHandler OnASWCInfo;
        public event PacketHandler OnPacket;

        public virtual void OnIntroReceived(PacketEventArgs e) { OnIntro(this, e); }
        public virtual void OnAckReceived(EventArgs e) { OnAck(this, e); }
        public virtual void OnFinalReceived(EventArgs e) { OnFinal(this, e); }
        public virtual void OnASWCInfoReceieved(EventArgs e) { OnASWCInfo(this, e); }
        public virtual void OnPacketReceieved(PacketEventArgs e) { OnPacket(this, e); }
        #endregion 

        /// <summary>
        /// Explicit implementations of the IAxxessBoard interface.  
        /// Note that ASWC is not available on CDC boards.
        /// </summary>
        #region Explicit IAxxessDevice Implementation
        string IAxxessBoard.ProductID { get { return this.ProductID; } }
        string IAxxessBoard.AppFirmwareVersion { get { return this.AppFirmwareVersion; } }
        string IAxxessBoard.BootFirmwareVersion { get { return this.BootFirmwareVersion; } }
        string IAxxessBoard.Info { get { return this.Info; } }
        ASWCInfo IAxxessBoard.ASWCInformation { get { return this.ASWCInformation; } }
        BoardType IAxxessBoard.Type { get { return this.Type; } }
        int IAxxessBoard.PacketSize { get { return this.PacketSize; } }

        byte[] IAxxessBoard.PrepPacket(byte[] packet) { return this.PrepPacket(packet); }
        byte[] IAxxessBoard.IntroPacket { get { return this.IntroPacket; } }
        byte[] IAxxessBoard.ReadyPacket { get { return this.ReadyPacket; } }
        
        void IAxxessBoard.SendIntroPacket() { this.SendIntroPacket(); }
        void IAxxessBoard.SendReadyPacket() { this.SendReadyPacket(); }
        void IAxxessBoard.SendASWCMappingPacket(ASWCInfo map, IList<SectionChanged> list) { throw new NotImplementedException("Mapping not yet implemented for CDC!"); }
        void IAxxessBoard.SendASWCRequestPacket() { this.SendASWCRequestPacket(); }
        void IAxxessBoard.SendPacket(byte[] packet) { this.Write(packet); }
        void IAxxessBoard.SendRawPacket(byte[] packet) { this.Write(packet); }
        byte IAxxessBoard.CalculateChecksum(byte[] packet) { return this.CalculateChecksum(packet); }

        void IAxxessBoard.AddIntroEvent(IntroEventHandler handler) { this.OnIntro += handler; }
        void IAxxessBoard.RemoveIntroEvent(IntroEventHandler handler) { this.OnIntro -= handler; }
        void IAxxessBoard.AddAckEvent(AckEventHandler handler) { this.OnAck += handler; }
        void IAxxessBoard.RemoveAckEvent(AckEventHandler handler) { this.OnAck -= handler; }
        void IAxxessBoard.AddFinalEvent(FinalEventHandler handler) { this.OnFinal += handler; }
        void IAxxessBoard.RemoveFinalEvent(FinalEventHandler handler) { this.OnFinal -= handler; }
        void IAxxessBoard.AddRemovedEvent(EventHandler handler) { this.OnDeviceRemoved += handler; }
        void IAxxessBoard.RemoveRemovedEvent(EventHandler handler) { this.OnDeviceRemoved -= handler; }
        void IAxxessBoard.AddASWCInfoEvent(ASWCInfoHandler handler) { this.OnASWCInfo += handler; }
        void IAxxessBoard.RemoveASWCInfoEvent(ASWCInfoHandler handler) { this.OnASWCInfo -= handler; }
        void IAxxessBoard.AddPacketEvent(PacketHandler handler) { this.OnPacket += handler; }
        void IAxxessBoard.RemovePacketEvent(PacketHandler handler) { this.OnPacket -= handler; }

        void IAxxessBoard.AddASWCConfimEvent(ASWCConfirmHandler handler)
        {
            throw new NotImplementedException();
        }

        void IAxxessBoard.RemoveASWCConfimEvent(ASWCConfirmHandler handler)
        {
            throw new NotImplementedException();
        }

        void IAxxessBoard.Dispose() { this.Dispose(); }

        #endregion
    }
}
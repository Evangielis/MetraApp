﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Metra.Axxess
{
    public enum PacketType
    {
        IntroPacket,
        ReadyPacket,
    };

    public enum BoardStatus
    {
        NoOp,
        Hailed,
        Idling,
        Ready,
        Standby,
        Finalizing,
    };

    public enum BoardType
    {
        HIDChecksum,
        HIDNoChecksum,
    };

    public abstract class AxxessBoard : HIDDevice
    {
        //Board attributes
        public int ProductID { get; protected set; }
        public int AppFirmwareVersion { get; protected set; }
        public int BootFirmwareVersion { get; protected set; }
        public BoardStatus Status { get; protected set; }

        public byte[] IntroPacket { get; protected set; }
        public byte[] ReadyPacket { get; protected set; }

        public BoardType Type { get; protected set; }

        public AxxessBoard() : base() 
        { 
            this.ProductID = 0; 
            this.AppFirmwareVersion = 0;
            this.BootFirmwareVersion = 0;
            this.Status = BoardStatus.NoOp;
        }

        public void SendIntroPacket() 
        { 
            this.Write(new IntroReport(this));
            this.Status = BoardStatus.Hailed;
            Thread.Sleep(200);
        }
        public void SendReadyPacket() 
        { 
            this.Write(new ReadyReport(this));
            this.Status = BoardStatus.Standby;
        }

        public virtual bool IsAck(byte[] packet) { return false; }
        public virtual bool IsFinal(byte[] packet) { return false; }

        //Packet manipulations
        public virtual byte[] PrepPacket(byte[] packet) { return packet; }
        protected virtual void ProcessIntroPacket(byte[] packet) { return; }

        //Functional Logic
        public virtual void UpdateAppFirmware(string path, ToolStripProgressBar bar) { return; }

        #region Statics
        public static AxxessBoard ConnectToBoard()
        {
            AxxessBoard device;
            device = (AxxessBoard)HIDDevice.FindDevice(1240, 63301, typeof(HIDChecksumBoard));

            return device;
        }
        #endregion
    }

    /// <summary>
    /// Class based implementatin of the HID w/ Checksum board
    /// </summary>
    public class HIDChecksumBoard : AxxessBoard
    {
        
        
        public HIDChecksumBoard()
            : base()
        {
            this.Type = BoardType.HIDChecksum;
            this.IntroPacket = this.PrepPacket(new byte[] { 0x01, 0xF0, 0x10, 0x03, 0xA0, 0x01, 0x0F, 0x58, 0x04 });
            this.ReadyPacket = this.PrepPacket(new byte[] { 0x01, 0xF0, 0x20, 0x00, 0xEB, 0x04 });
        }

        public override InputReport CreateInputReport()
        {
            return new TestInputReport(this);
        }

        protected override void HandleDataReceived(InputReport oInRep)
        {
            //Test code block
            //foreach (byte b in oInRep.Buffer) { Console.Out.Write(b + " "); }
            //Console.Out.Write("\n");

            //How we handle packets depends on what the status of the board is
            switch(this.Status)
            {
                //If we're not expecting anything from the board we just ignore incoming.
                case BoardStatus.NoOp:
                    return;

                //If we've hailed the board with an intro packet we'll be looking for version info.
                case BoardStatus.Hailed:
                    if (ParseIntroPacket(oInRep.Buffer))
                        this.Status = BoardStatus.Idling;
                    break;

                case BoardStatus.Idling:
                    return;

                case BoardStatus.Standby:
                    if (IsAck(oInRep.Buffer))
                        this.Status = BoardStatus.Ready;
                    else if (IsFinal(oInRep.Buffer))
                        this.Status = BoardStatus.Finalizing;
                    break;

                case BoardStatus.Ready:
                    return;

                case BoardStatus.Finalizing:
                    return;
            }
        }

        /// <summary>
        /// Method to extract board versioning info from intro response packets
        /// </summary>
        /// <param name="packet">The received packet</param>
        /// <returns>True of intro packet, else false</returns>
        protected bool ParseIntroPacket(byte[] packet)
        {
            //Parse packet into characters
            string content = String.Empty;
            foreach (byte b in packet)
            {
                content += Convert.ToChar(b);
            }
            
            if (content.Substring(10,3).Equals("CWI"))
            {
                this.ProductID = Convert.ToInt32(content.Substring(13, 7));
                try
                {
                    this.AppFirmwareVersion = Convert.ToInt32(content.Substring(29, 3));
                }
                catch (FormatException e)
                {
                    this.AppFirmwareVersion = 0;
                }
                return true;
            }
            else { return false; }
        }

        public override bool IsAck(byte[] packet) 
        {
            return ((packet[4] == 0x41)
                || (packet[5] == 0x41)
                || (packet[6] == 0x41));
        
        }
        public override bool IsFinal(byte[] packet)
        {
            return (packet[4] == 0x38);
        }

        /// <summary>
        /// Takes a packet and prepares it for transmission to the board.  Adds leading bytes, padding and checksum.
        /// </summary>
        /// <param name="packet">The packet to prepare for sending.</param>
        /// <returns></returns>
        public override byte[] PrepPacket(byte[] packet)
        {
            byte[] newPacket = new byte[65];
            byte[] content = base.PrepPacket(packet);

            //Add leading header
            newPacket[1] = 0x55;
            newPacket[2] = 0xB0;

            //Content length byte added
            newPacket[3] = Convert.ToByte(content.Length);

            //Add content bytes
            for (int i=0; i<content.Length; i++)
                newPacket[i + 4] = content[i];

            byte checksum = new byte();
            foreach (byte b in newPacket)
                checksum ^= b;
            newPacket[64] = checksum;

            return newPacket;
        }

        public void IntroSpam()
        {
            while (this.Status.Equals(BoardStatus.Idling) || this.Status.Equals(BoardStatus.Hailed))
            {
                this.SendIntroPacket();
                Thread.Sleep(200);
            }
        }

        public void SpinupIntro()
        {
            ThreadStart work = new ThreadStart(IntroSpam);
            Thread nThread = new Thread(work);
            nThread.Start();
        }

        public override void UpdateAppFirmware(string path, ToolStripProgressBar bar)
        {
            base.UpdateAppFirmware(path, bar);

            if (bar != null)
            {
                bar.Value = 0;
                bar.Text = "Querying device...";
            }

            //Listen to board
            int counter = 0;
            while (this.ProductID == 0)
            {
                try
                {
                    Console.Out.WriteLine("Sending intro packet...");
                    this.SendIntroPacket();
                    System.Threading.Thread.Sleep(200);
                    counter++;
                }
                catch (Exception e)
                {
                    Console.Out.WriteLine(e.Message.ToString());
                    System.Threading.Thread.Sleep(1000);
                }

                if (counter == 10)
                {
                    MessageBox.Show("Device not responding.  Please try disconnecting the device and restarting this application.", "Metra App for Windows");
                    return;
                }
            }


            //Set board status to standby
            this.Status = BoardStatus.Standby;

            //Preprocess the hexfile into a queue of 44-byte packets
            byte[] hexfile = File.ReadAllBytes(path);
            Queue<byte[]> packetQueue = new Queue<byte[]>();

            for (int i=0; i<(hexfile.Length/44); i++)
            {
                byte[] packet = new byte[44];
                for (int j=0; j<44; j++)
                {
                    packet[j] = hexfile[((i * 44) + j)];
                }
                packetQueue.Enqueue(packet);
            }

            //Send ready packet and wait for ack
            Console.WriteLine("Sending ready packet!");
            Console.WriteLine("Waiting for ack!");
            this.SendReadyPacket();
            while (!this.Status.Equals(BoardStatus.Ready))
            {
                Thread.Sleep(10);
            }

            //Send packets
            Console.WriteLine("Board is ready, preparing to stream file...");
            counter = 0;
            int maxval = packetQueue.Count;
            int stepval = maxval / 100;
            int progress = 0;

            while(packetQueue.Count > 0)
            {
                this.Status = BoardStatus.Standby;
                this.Write(new GenericReport(this, packetQueue.Dequeue()));
                counter++;
                while (this.Status.Equals(BoardStatus.Standby))
                {
                    //Console.WriteLine("Waiting for ack!");
                    Thread.Sleep(10);
                    //bar.Value = bar.Value + 1;
                }
                if ((counter % stepval) == 0)
                {
                    Console.WriteLine(progress + "%");
                    bar.Value = progress;
                    progress++;
                }
                if (this.Status.Equals(BoardStatus.Finalizing))
                    break;
            }

            Console.WriteLine("100%");
            Console.WriteLine("Board firmware update completed!");
            //form.Close();
            MessageBox.Show("Firmware installation completed!");
        }
    }
}

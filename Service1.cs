using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Timers;
using System.IO.Ports;
using Modbus.Device;
using System.Net.Sockets;
using System.Xml;

namespace AIBusDeviceServer
{
    public enum WorkState
    {
        send,
        receive
    }

    public enum ErrorCode
    {
        NONE,
        TIMEOUT,
        CHECK_FAILED,
        OTHER
        
    }
    public partial class Service1 : ServiceBase
    {
        private SerialPort serialPort;        
        private Timer timer;
        private TcpListener slaveTcpListener;
        private ModbusSlave slave;
        private byte[] sendBuffer;
        private int sendLength;
        private byte[] recvBuffer;
        private int recvLength;
        private WorkState aiWorkState;
        private int index;
        private int slaveFirstAddress;
        private int slaveCounts;
        private int timeOut;
        private const int timerInterval = 100;
        private int timerCounter;
        private int timerOutCounts;
        private ErrorCode commErrorCode;        
        public Service1() {
            InitializeComponent();
            
            sendBuffer = new byte[255];
            recvBuffer = new byte[255];            

            timer = new Timer();
            timer.Interval = 100;
            timer.AutoReset = true;
            timer.Elapsed +=new System.Timers.ElapsedEventHandler(timer_Elapsed);
            serialPort = new SerialPort();

            System.Net.IPAddress addr = new System.Net.IPAddress(new byte[] { 127, 0, 0, 1 });
            int port = 502;
            slaveTcpListener = new TcpListener(addr, port);

            slave = ModbusTcpSlave.CreateTcp(1, slaveTcpListener);
            slave.DataStore = Modbus.Data.DataStoreFactory.CreateDefaultDataStore(1, 1, 1, 21);
            slave.DataStore.InputRegisters.Add(3);
            
        }

        protected override void OnStart(string[] args) {
            XmlDocument config = new XmlDocument();
            //XmlReaderSettings set = new XmlReaderSettings();
            //set.IgnoreComments = true;
            //XmlReader reader = XmlReader.Create(@"C:\AIBUS\config.xml", set);
            //config.Load(reader);
            config.Load(@"C:\AIBUS\config.xml");
            XmlNode node = config.SelectSingleNode("/global/serialPort");
            node = node.FirstChild;
            serialPort.PortName = node.InnerText;
            node = node.NextSibling;
            serialPort.DataBits = Convert.ToInt32(node.InnerText);
            node = node.NextSibling;
            string parity = node.InnerText;
            if (string.Equals(parity, "none", StringComparison.OrdinalIgnoreCase) == true) {
                serialPort.Parity = Parity.None;
            } else if (string.Equals(parity, "odd", StringComparison.OrdinalIgnoreCase) == true) {
                serialPort.Parity = Parity.Odd;
            } else if (string.Equals(parity, "even", StringComparison.OrdinalIgnoreCase) == true) {
                serialPort.Parity = Parity.Even;
            }
            node = node.NextSibling;
            string stopBits = node.InnerText;
            if (stopBits == "1") {
                serialPort.StopBits = StopBits.One;
            } else {
                serialPort.StopBits = StopBits.Two;
            }
            node = node.NextSibling;
            timeOut = Convert.ToInt32(node.InnerText);
            timerOutCounts = timeOut / timerInterval;

            node = config.SelectSingleNode("/global/AIDevice");
            node = node.FirstChild;
            slaveFirstAddress = Convert.ToInt32(node.InnerText);
            node = node.NextSibling;
            slaveCounts = Convert.ToInt32(node.InnerText);
            index = 0;
            timerCounter = 0;
            aiWorkState = WorkState.send;
            serialPort.Open();
            timer.Start();

            slaveTcpListener.Start();
            slave.Listen();
        }

        protected override void OnStop() {
            timer.Stop();
            serialPort.Close();
            //slaveTcpListener.Stop();
        }
        private void timer_Elapsed(object sender, ElapsedEventArgs e) {
            int i = 0;
            short checksum = 0;
            if (aiWorkState == WorkState.send) {
                i = 0;
                sendBuffer[i++] = Convert.ToByte(0x80 + slaveFirstAddress + index);
                sendBuffer[i++] = Convert.ToByte(0x80 + slaveFirstAddress + index);
                sendBuffer[i++] = 0x52;
                sendBuffer[i++] = 0x00;
                sendBuffer[i++] = 0x00;
                sendBuffer[i++] = 0x00;
                checksum = Convert.ToInt16(sendBuffer[3] * 256 + slaveFirstAddress + index + 0x52);
                sendBuffer[i++] = Convert.ToByte(checksum & 0xff);
                sendBuffer[i++] = Convert.ToByte((checksum >> 8) & 0xff);
                sendLength = i;
                serialPort.DiscardInBuffer();
                serialPort.Write(sendBuffer, 0, sendLength);
                aiWorkState = WorkState.receive;
                timerCounter = 0;
            } else if (aiWorkState == WorkState.receive) {
                if (timerCounter < timerOutCounts) {
                    if (serialPort.BytesToRead >= 10) {
                        recvLength = serialPort.BytesToRead;
                        serialPort.Read(recvBuffer, 0, 10);                        
                        checksum = Convert.ToInt16(recvBuffer[8] + (recvBuffer[9] << 8));
                        int checksumCac = recvBuffer[5] * 256;
                        checksumCac += recvBuffer[0];
                        checksumCac += (recvBuffer[1] << 8);
                        checksumCac += recvBuffer[2];
                        checksumCac += (recvBuffer[3] << 8);
                        checksumCac += recvBuffer[4];
                        checksumCac += recvBuffer[6];
                        checksumCac += (recvBuffer[7] << 8);
                        checksumCac += Convert.ToInt32(slaveFirstAddress + index);
                        short temp = Convert.ToInt16(checksumCac & 0xffff);
                        float result;
                        if (temp == checksum) {
                            result = Convert.ToSingle((recvBuffer[0] + (recvBuffer[1] << 8)) * 0.1);
                            commErrorCode = ErrorCode.NONE;
                            byte[] resultByte = BitConverter.GetBytes(result);
                            int arrIndex = (index + 1) * 2;
                            slave.DataStore.InputRegisters[arrIndex] = Convert.ToUInt16((resultByte[3] << 8) + resultByte[2]);
                            slave.DataStore.InputRegisters[arrIndex + 1] = Convert.ToUInt16((resultByte[1] << 8) + resultByte[0]);
                        } else {
                            commErrorCode = ErrorCode.CHECK_FAILED;
                        }
                        index++;
                        aiWorkState = WorkState.send;
                        
                    }
                } else {
                    index++;
                    commErrorCode = ErrorCode.TIMEOUT;
                    aiWorkState = WorkState.send;
                    
                }
            }
            slave.DataStore.InputRegisters[1] = Convert.ToUInt16(commErrorCode);
            timerCounter++;
            if (index >= slaveCounts) {
                index = 0;
            }
        }

        
    
    }
}

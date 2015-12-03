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

namespace AIBusDeviceServer
{
    public partial class Service1 : ServiceBase
    {
        //private string serialPortName;
        //private StopBits stopBits;
        //private Parity parity;
        private SerialPort serialPort;        
        private Timer timer;
        private TcpListener slaveTcpListener;
        private ModbusSlave slave;
        public Service1() {
            InitializeComponent();
            timer = new Timer();
            timer.Interval = 1000;
            timer.AutoReset = true;
            timer.Elapsed +=new System.Timers.ElapsedEventHandler(timer_Elapsed);
            serialPort = new SerialPort("COM2", 9600, Parity.None, 8, StopBits.One);

            System.Net.IPAddress addr = new System.Net.IPAddress(new byte[] { 127, 0, 0, 1 });
            int port = 502;
            slaveTcpListener = new TcpListener(addr, port);

            slave = ModbusTcpSlave.CreateTcp(1, slaveTcpListener);
            slave.DataStore = Modbus.Data.DataStoreFactory.CreateDefaultDataStore(10, 10, 10, 10);
            slave.DataStore.InputRegisters[1] = 100;
            slave.DataStore.InputRegisters[2] = 200;
            slave.DataStore.InputRegisters[3] = 300;
            slave.Listen();
        }

        protected override void OnStart(string[] args) {            
            serialPort.Open();
            timer.Start();
            slaveTcpListener.Start();
        }

        protected override void OnStop() {
            timer.Stop();
            serialPort.Close();
            slaveTcpListener.Stop();
        }
        private void timer_Elapsed(object sender, ElapsedEventArgs e) {
            serialPort.WriteLine("hello world From Windows Service, WoW!!!\n");
        }
    
    }
}

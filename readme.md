﻿##AI Bus Device Server
 基于.Net FrameWork 4.0
 引用NModbus4
 通过电脑上的串口采集厦门宇电温度巡检仪的温度，转换为Modbus TCP Slave供SCADA等程序读取。
 最多支持10个子站
 ModbusTCPSlave 地址表，04功能码区域，地址从1开始
 |Address|Name  |Data Type|       |
 |:------|:-----|:--------|:------|
 |01|CommStatus|int|0:OK  1:timeout  2:check failed  3:unknow|
 |02|channel 1 temperature|float||
 |04|channel 2 temperature|float||
 |06|channel 3 temperature|float||
 |08|channel 4 temperature|float||
 |10|channel 5 temperature|float||
 |12|channel 6 temperature|float||
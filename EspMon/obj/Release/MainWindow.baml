   M S B A M L   `   `   `  ���� A  =EspMon, Version=0.1.9.0, Culture=neutral, PublicKeyToken=nullclr-namespace:EspMonEspMon      EspMon.MainWindow   R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35D 9http://schemas.microsoft.com/winfx/2006/xaml/presentation    R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     @d2http://schemas.microsoft.com/expression/blend/2008     Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     &localclr-namespace:EspMon     -   $	��300��$	��200��  � Title$  	Esp Mon 3��$	��200��$	��300��.���  � ShowGridLines .  $��3q� � ColumnDefinitions �� $��1* ��� $��1* ��� $��1* ��� $��1* � � RowDefinitions �� $
��Auto ��� $
��Auto ��� $
��Auto ��� $��* ��� $
��Auto ��� $
��Auto �.+��� -    ��Name$ cpuTmaxLabel��$��	CPU TMax:��$
��Left=�$��0��y��� *MainVisibility+�� -    ��Name$ cpuTmax��$��Center=�$��1��$��3q����� *	CpuTMax+y��� *MainVisibility+�� -   $ gpuTmaxLabel��$��	GPU TMax:��$
��Left=�$��2��y��� *MainVisibility+�� -   $ gpuTmax��$��Center=�$��3��$��3q����� *	GpuTMax+y��� *MainVisibility+�� -   $ comPortsLabel��$��
COM Ports:��$��0��$��1��y��� *MainVisibility+�� -    ��Name$ comPortsRefresh��$��Refresh��$
��Left=�$��1��$��1��$��3q�y��� *MainVisibility+]� $��2��$��1��$��2��$��2��$��3q�y��� *MainVisibility+.+��� -    ��Name$ isStartedCheckbox��$��Started��$	��Top=� d� 	IsChecked �� *	IsStarted+�� -	   $ serviceInstalledButton��$��
Persistent��$��Bottom=� �� *IsPersistent+�� -
   	 ��Name$	 comPortsList��$��3q�$��3��$��4������ *	PortItems+y��� *MainVisibility+���� .R�]� .+��� $
��Left=� �� *	IsChecked+���� *Name+�� -   $ flashDevice��$��Flash Device >��$��4��$��3q�$��4��y��� *FlashButtonVisibility+�� -   $ comPortLabel��$��	COM Port:��$��3q�$��0��$��0��$��Right=�y��� *FlashingVisibility+�� -   
 ��Name$
 comPortCombo��$��3q�$��1��$��0��$��2��y��� *FlashingVisibility+{��� *IsIdle+�� -   $ refreshComPortCombo��$��Refresh��$��3q�$��0��$��3��y��� *FlashingVisibility+{��� *IsIdle+�� -   $ deviceLabel��$��Device:��$��3q�$��0��$��1��$��Right=�y��� *FlashingVisibility+�� -   $
 deviceCombo��$��3q�$��1��$��1��$��2��y��� *FlashingVisibility+{��� *IsIdle+�� -   $ flashDeviceButton��$��Flash��$��3q�$��1��$��3��y��� *FlashingVisibility+{��� *IsIdle+� $��2��$��4�� � Minimum$ 0Q� � Maximum$	 100Q� � Value �� *FlashProgress+y��� *FlashingVisibility+�� -   $ output�� �� 
IsReadOnly .  �� TextWrapping$
 Wrap=�$��3q�" �� VerticalScrollBarVisibility$ Visible=�$��3��$��4��y��� *FlashingVisibility+���� *
OutputText+�� -   $
 back��$��Back <��$��3q�$��4��$��4��y��� *FlashingVisibility+{��� *IsIdle+�� -    ��Name$ 
mainStatus��$��5��$��4��.@��� -   $ updateButton��$��Install latest version...��y��� *UpdateVisibility+{��� *IsIdle+
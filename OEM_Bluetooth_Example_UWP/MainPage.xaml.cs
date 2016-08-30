using System;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using System.Threading.Tasks;
using System.Threading;
using Windows.Devices.Enumeration;

using System.Diagnostics;

using CRCSpace;


namespace OEM_Bluetooth_Example_UWP
{
    public sealed partial class MainPage : Page
    {
        private DeviceInformationCollection deviceCollection;
        private DeviceInformation selectedDevice;
        private RfcommDeviceService deviceService;

        private DataWriter dataWriterObject;
        private DataReader dataReaderObject;
        private CancellationTokenSource ReadCancellationTokenSource;

        public string deviceName = "Wasatch-D70B";

        private StreamSocket streamSocket;

        public bool connected = false;
        CRC Crc;

        const int CMDFrontOverhead = 3;
        const int CMDBackendOverhead = 2;
        const int CMDOverhead = 5;
        const int CMDIndex = 3;

        byte[] nullData = new byte[0];

        byte[] outputSpectra = new byte[2048];
        int outputSpectraIndex = 0;
        string outputSpectraString = "";

        // Command protocol for Spectrometer
        enum DeviceCommand
        {
            AcquireImage = 0x0A,
            ReadFirmwareRevision = 0x0D,
            ReadFPGARevision = 0x10,
            IntegrationTime = 0x11,
            CCDSignalOffset = 0x13,
            CCDSignalGain = 0x14,
            ReadPixelCount = 0x15,
            CCDTemperatureSetpoint = 0x16,
            LaserModulationDuration = 0x17,
            LaserModulationPulseDelay = 0x18,
            LaserModulationPeriod = 0x19,
            LaserModulationPulseWidth = 0x1E,
            GetActualIntegrationTime = 0x1F,
            GetActualFrameCount = 0x20,
            TriggerDelay = 0x28,
            OutputTestPattern = 0x30,
            SelectUSBFullSpeed = 0x32,
            LaserModulation = 0x33,
            LaserOn = 0x34,
            CCDTemperatureEnable = 0x38,
            LaserModLinkToIntegrationTime = 0x39,
            CCDTemperature = 0x49,
            PassFailLED = 0x4A,
            ConnectionPing = 0x4B,
            ClearAcquireButtonPressed = 0x4C,
            Write = 0x80                            // Write opperations for the above commands
        }                                           // is the read command, plus 0x80

        private byte lastDeviceCommand;

        enum StrokerResponse
        {
            Busy = -4,
            InternalAddressInvalid,
            InternalCommunicationFailure,
            InternalDataError,
            Success,
            LengthError,
            CRCError,
            UnrecognizedCommand,
            PortNotAvailable
        }

        enum BlueToothCommErrors
        {
            ConnectionFailed = 1,
        }

        public MainPage()
        {
            this.InitializeComponent();

            Crc = new CRC();
            InitializeRfcommServer();

            dateStampOutput();
            outputTextBox.Text += "Initialized" + System.Environment.NewLine;
        }

        public void dateStampOutput()
        {
            System.DateTime dateTimeNow = DateTime.Now;
            string printDateNow = dateTimeNow.ToString();
            outputTextBox.Text += System.Environment.NewLine + printDateNow + " : ";
        }

        /// <summary>
        /// Kicks off the connection routine. 
        /// Disables the connect button so that we can't execute the routine again
        /// until we are ready.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonConnect_Click(object sender, RoutedEventArgs e)
        {
            buttonConnect.IsEnabled = false;
            deviceName = comboBoxAvailableDevices.SelectedItem.ToString();
            Debug.WriteLine(deviceName);
            ConnectToDevice();
        }

        private async void InitializeRfcommServer()
        {
            var selector = BluetoothDevice.GetDeviceSelector();
            var devices = await DeviceInformation.FindAllAsync(selector);

            comboBoxAvailableDevices.Items.Clear();

            foreach (var device in devices)
            {
                comboBoxAvailableDevices.Items.Add(device.Name);
            }
            try
            {
                string device1 = RfcommDeviceService.GetDeviceSelector(RfcommServiceId.SerialPort);
                deviceCollection = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(device1);
            }
            catch (Exception exception)
            {
                dateStampOutput();
                outputTextBox.Text += "InitializeRfcommServer Exception: " + exception.ToString() + System.Environment.NewLine;
            }
        }

        async void refreshRfcommDeviceService()
        {
            try
            {
                DeviceInformationCollection DeviceInfoCollection = await DeviceInformation.FindAllAsync(RfcommDeviceService.GetDeviceSelector(RfcommServiceId.SerialPort));
                var numDevices = DeviceInfoCollection.Count();
                if (numDevices == 0)
                {
                    System.Diagnostics.Debug.WriteLine("InitializeRfcommDeviceService: No paired devices found.");
                }
                else
                {

                }

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("InitializeRfcommDeviceService: " + ex.Message);
            }
        }

        /// <summary>
        /// Connects to bluetooth device designated by the AvailableDevices comboBox
        /// </summary>
        private async void ConnectToDevice()
        {
            bool success = true;

            foreach (var item in deviceCollection)
            {
                if (item.Name == deviceName)
                {
                    selectedDevice = item;
                    break;
                }
            }

            if (selectedDevice == null)
            {
                dateStampOutput();
                outputTextBox.Text += "Cannot find specified device." + System.Environment.NewLine;
                return;
            }
            else
            {
                // Attempt to open device stream
                try
                {
                    deviceService = await RfcommDeviceService.FromIdAsync(selectedDevice.Id);

                    // Disposing the socket with close it and release all resources 
                    // associated with the socket
                    if (streamSocket != null)
                    {
                        streamSocket.Dispose();
                    }
                    streamSocket = new StreamSocket();

                    // Connect to socket
                    // NOTE: If either paramter is null or empty, the call will throw an exception.
                    //       If you are using this in a new application and this keeps throwing an exception,
                    //       then you have not added the Bluetooth Capabilities to your app. Double click
                    //       on the Package.appxmanifest file in the Solution Explorer. It's the third tab.
                    try
                    {
                        await streamSocket.ConnectAsync(deviceService.ConnectionHostName, deviceService.ConnectionServiceName);
                    }
                    catch (Exception ex)
                    {
                        success = false;
                        dateStampOutput();
                        outputTextBox.Text += "Cannot connect to bluetooth device: " + ex.Message + System.Environment.NewLine;
                        dateStampOutput();
                        outputTextBox.Text += "NOTE: Make sure Bluetooth capabilities are added in your Package.appxmanifest file.";
                    }

                    if (success)
                    {
                        dateStampOutput();
                        outputTextBox.Text += "Successfully connected to bluetooth device" + System.Environment.NewLine;
                        Listen();
                        testConnection();
                    }
                    else
                    {
                        dateStampOutput();
                        outputTextBox.Text += "Failed to connect to bluetooth device" + System.Environment.NewLine;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Overall connect: " + ex.Message);

                    dateStampOutput();
                    outputTextBox.Text += "CONNECTION EXCEPTION: " + ex.Message + System.Environment.NewLine;

                    streamSocket.Dispose();
                    streamSocket = null;
                }
            }
        }

        /// <summary>
        /// This is where all of the returned data parsing is done.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task ReadAsync(CancellationToken cancellationToken)
        {
            Task<UInt32> loadAsyncTask;

            // If task cancellation was requested, comply
            cancellationToken.ThrowIfCancellationRequested();

            // Set InputStreamOptions to complete the asynchronous read operation when one or more bytes is available
            dataReaderObject.InputStreamOptions = InputStreamOptions.Partial;

            // Create a task object to wait for data on the serialPort.InputStream
            loadAsyncTask = dataReaderObject.LoadAsync(8192).AsTask(cancellationToken);

            // Launch the task and wait
            UInt32 bytesRead = await loadAsyncTask;

            if (bytesRead > 0)
            {
                try
                {
                    byte[] response = new byte[bytesRead];
                    UInt32 j = 0;
                    uint k = 0;
                    //System.Diagnostics.Debug.WriteLine(bytesRead.ToString());
                    dataReaderObject.ReadBytes(response);

                    // Validate our data
                    // 1 - Make sure that we have the minimum size reply
                    // 2 - Make that both of our delimiters are there
                    // 3 - Make sure that it is our intended packet type
                    //
                    // If any of these fail, send the initial request again
                    // 
                    // NOTE: The return spectrum bypasses this because it has no 
                    // delimiter and overall format on the return. Also, the bluetooth
                    // module doesn't always transmit it all in a single chunk but splits it into
                    // multiple transmissions. This is why it's handled differently.
                    if (lastDeviceCommand == (byte)DeviceCommand.AcquireImage)
                    {
                        outputSpectraIndex = outputSpectraIndex + (int)bytesRead;
                        byte[] intensityArray = { 0, 0 };
                        // Assemble and print out our recieved spectra
                        for (int i = 0; i < bytesRead; i++)
                        {
                            if (k == 1)
                            {
                                intensityArray[0] = response[i-1];
                                intensityArray[1] = response[i];
                                outputSpectraString = outputSpectraString + BitConverter.ToInt16(intensityArray, 0) + " ";
                                k = 0;
                            }
                            else
                            {
                                k++;
                            }
                        }
                        dateStampOutput();
                        outputTextBox.Text += outputSpectraString + System.Environment.NewLine;
                        enableAllAcquisitionButtons();
                        return;
                    }

                    // Only partially recieved packet, resend data request
                    if (bytesRead < 6)
                    {
                        SendData_Click((DeviceCommand)lastDeviceCommand, 0, nullData);
                        return;
                    }

                    // First and last bytes should be delimiters "<" = d60 and ">" = d62
                    if (response[0] != 60 || response[bytesRead - 1] != 62)
                    {
                        SendData_Click((DeviceCommand)lastDeviceCommand, 0, nullData);
                        return;
                    }

                    // Returned packet type should match what we expect
                    if (response[3] != lastDeviceCommand)
                    {
                        SendData_Click((DeviceCommand)lastDeviceCommand, 0, nullData);
                        return;
                    }
                    
                    // Print out the exact data recieved to the terminal
                    string str = "";
                    for (int i = 0; i < bytesRead; i++)
                    {
                        str = str + " " + response[i].ToString();
                    }
                    dateStampOutput();
                    outputTextBox.Text += "Data Recieved: " + str + System.Environment.NewLine;

                    // Master case statement to process individual commands.
                    // Only three are presented as an example.
                    switch (lastDeviceCommand)
                    {

                        case (byte)DeviceCommand.ReadFirmwareRevision:
                            byte[] firmwareRevisionBuffer = new byte[5];
                            k = 0;
                            for (uint i = (bytesRead - 7); i < bytesRead - 2; i++)
                            {
                                firmwareRevisionBuffer[k] = response[i];
                                k++;
                            }
                            dateStampOutput();
                            outputTextBox.Text += "Firmware revision  -  " 
                                                + firmwareRevisionBuffer[1].ToString() + "."
                                                + firmwareRevisionBuffer[2].ToString() + "."
                                                + firmwareRevisionBuffer[3].ToString() + "."
                                                + firmwareRevisionBuffer[4].ToString() 
                                                + "  -  returned from device. " + System.Environment.NewLine;
                            enableAllAcquisitionButtons();
                            break;

                        case (byte)DeviceCommand.ReadFPGARevision:
                            byte[] fpgaRevisionNumberBuffer = new byte[7];
                            k = 0;
                            for (uint i = (bytesRead - 9); i < bytesRead - 2; i++)
                            {
                                fpgaRevisionNumberBuffer[k] = response[i];
                                k++;
                            }
                            dateStampOutput();
                            str = System.Text.Encoding.ASCII.GetString(fpgaRevisionNumberBuffer);
                            outputTextBox.Text += "FPGA revision  -  " + str + "  -  returned from device. " + System.Environment.NewLine;
                            enableAllAcquisitionButtons();
                            break;

                        case (byte)DeviceCommand.ConnectionPing:
                            System.Diagnostics.Debug.WriteLine("Ping returned from device");
                            enableAllAcquisitionButtons();
                            dateStampOutput();
                            outputTextBox.Text += "Ping returned from device " + System.Environment.NewLine;
                            break;

                        default:
                            break;
                    }               
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("ReadAsync: " + ex.Message);

                    dateStampOutput();
                    outputTextBox.Text += "ReadAsync EXCEPTION: " + ex.Message + System.Environment.NewLine;
                }

            }
        }

        /// <summary>
        /// Cancel read
        /// </summary>
        private void CancelReadTask()
        {
            if (ReadCancellationTokenSource != null)
            {
                if (!ReadCancellationTokenSource.IsCancellationRequested)
                {
                    ReadCancellationTokenSource.Cancel();
                }
            }
        }


        /// <summary>
        /// Assemble packet and send off to device.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="write"></param>
        /// <param name="dataT"></param>
        /// <returns></returns>
        private async Task WriteAsync(DeviceCommand command, int write, byte[] dataT)
        {
            Task<UInt32> storeAsyncTask;

            byte[] cmd = new byte[CMDOverhead + dataT.Length];
            byte[] data = new byte[cmd.Length + 1];

            int dataArrayLength = dataT.Length;
            dataArrayLength = dataArrayLength + 1;

            // Assemble command information
            cmd[0] = 0;
            cmd[1] = (byte)dataArrayLength;
            if (write > 0)
            {
                byte temp = (byte)(command + 0x80);
                cmd[2] = temp;
            }
            else
                cmd[2] = (byte)command;
            if (dataArrayLength > 1)
            {
                for (int i = 0; i < dataT.Length; i++)
                {
                    cmd[i + 3] = dataT[i];
                }
                cmd[3 + dataArrayLength - 1] = Crc.CalculateCRC(cmd, 3 + dataT.Length);
            }
            else
                cmd[3] = Crc.CalculateCRC(cmd, 3);


            // add command data to packet
            cmd.CopyTo(data, 1);

            // add delimiters to packet
            data[0] = (byte)'<';
            data[data.Length - 1] = (byte)'>';

            // Print data to debug terminal
            dateStampOutput();
            outputTextBox.Text += "Data Sent: ";
            for (int j = 0; j < data.Length; j++)
                outputTextBox.Text += data[j].ToString() + " ";
            outputTextBox.Text += System.Environment.NewLine;

            // Load the packet into the dataWriter object
            dataWriterObject.WriteBytes(data);

            // Launch an async task to complete the write operation
            storeAsyncTask = dataWriterObject.StoreAsync().AsTask();

            UInt32 bytesWritten = await storeAsyncTask;

            if (bytesWritten > 0)
            {
                string status_Text = ", ";
                string debug_text = "";
                status_Text += bytesWritten.ToString();
                status_Text += " bytes written successfully!";

                for (int i = 0; i < data.Length; i++)
                {
                    debug_text = debug_text + data[i].ToString() + " ";
                }

                debug_text = debug_text + status_Text;
                Debug.WriteLine(debug_text);
            }
        }

        /// <summary>
        /// Watch for data coming back.
        /// </summary>
        private async void Listen()
        {
            try
            {
                ReadCancellationTokenSource = new CancellationTokenSource();
                if (streamSocket.InputStream != null)
                {
                    dataReaderObject = new DataReader(streamSocket.InputStream);
                    // keep reading the serial input
                    while (true)
                    {
                        await ReadAsync(ReadCancellationTokenSource.Token);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex.GetType().Name == "TaskCanceledException")
                {
                    System.Diagnostics.Debug.WriteLine("Listen: Reading task was cancelled, closing device and cleaning up");

                    dateStampOutput();
                    outputTextBox.Text += "LISTEN EXCEPTION: Reading task was cancelled, closing device and cleaning up." + System.Environment.NewLine;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Listen: " + ex.Message);

                    dateStampOutput();
                    outputTextBox.Text += "LISTEN EXCEPTION: " + ex.Message + System.Environment.NewLine;
                }
            }
            finally
            {
                // Cleanup once complete
                if (dataReaderObject != null)
                {
                    dataReaderObject.DetachStream();
                    dataReaderObject = null;
                }
            }
        }

        /// <summary>
        /// Tests the connection by sending a ping command to the instrument.
        /// </summary>
        private void testConnection()
        {
            lastDeviceCommand = (byte)DeviceCommand.ConnectionPing;
            SendData_Click(DeviceCommand.ConnectionPing, 0, nullData);
        }

        /// <summary>
        /// Writes out the commands to the device and handles exceptions as 
        /// appropriate.
        /// </summary>
        /// <param name="command">The command to be sent to the device</param>
        /// <param name="write">whether it is a write command</param>
        /// <param name="data">Data to be sent, if it is a write.</param>
        private async void SendData_Click(DeviceCommand command, int write, byte[] data)
        {
            try
            {
                if (streamSocket.OutputStream != null)
                {
                    // Create the DataWriter object and attach to OutputStream
                    dataWriterObject = new DataWriter(streamSocket.OutputStream);

                    // send off the data and wait
                    await WriteAsync(command, write, data);
                }
                else
                {
                    dateStampOutput();
                    outputTextBox.Text += "Bluetooth did not connect correctly. " + System.Environment.NewLine;
                }
            }
            catch (Exception ex)
            {
                dateStampOutput();
                outputTextBox.Text += "SEND ERROR: " + ex.Message + System.Environment.NewLine;
            }
            finally
            {
                // Cleanup once complete
                if (dataWriterObject != null)
                {
                    dataWriterObject.DetachStream();
                    dataWriterObject = null;
                }
            }
        }

        /// <summary>
        /// For cleanliness, the enable and disable functions below are used
        /// to handle the enabled state of the acquisition buttons so that
        /// we are not able to kick-off an additional bluetooth action before
        /// the previous was resolved. This conflict causes exceptions to 
        /// be thrown in the RFCOMM libraries.
        /// 
        /// For bulk transfers of GETs and SETs a state machine is needed
        /// so that the next action waits until the previous has finished
        /// both the send and read portions.
        /// </summary>
        private void enableAllAcquisitionButtons()
        {
            buttonPing.IsEnabled = true;
            buttonAcquire.IsEnabled = true;
            buttonFirmwareRev.IsEnabled = true;
            buttonFpgaRev.IsEnabled = true;
        }

        private void disableAllAcquistionButtons()
        {
            buttonPing.IsEnabled = false;
            buttonAcquire.IsEnabled = false;
            buttonFirmwareRev.IsEnabled = false;
            buttonFpgaRev.IsEnabled = false;
        }

        /// <summary>
        /// Refreshes the available bluetooth devices as reported by Windows. 
        /// If you do not see your device then check your bluetooth settings on
        /// your computer. Be sure that the device is present and paired.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void buttonRefresh_Click(object sender, RoutedEventArgs e)
        {
            var selector = BluetoothDevice.GetDeviceSelector();
            var devices = await DeviceInformation.FindAllAsync(selector);

            comboBoxAvailableDevices.Items.Clear();

            foreach (var device in devices)
            {
                comboBoxAvailableDevices.Items.Add(device.Name);
            }

            try
            {
                CancelReadTask();

                if (streamSocket != null)
                {
                    streamSocket.Dispose();
                }
            }
            catch
            {
                dateStampOutput();
                outputTextBox.Text += "Exception on disposing streamsocket" + System.Environment.NewLine;
            }

            disableAllAcquistionButtons();
            dateStampOutput();
            outputTextBox.Text += "Bluetooth device list refreshed." + System.Environment.NewLine;
        }

        /// <summary>
        /// Function enables the CONNECT button once we have changed the selection of the bluetooth
        /// device comboBox to something other than the placeholder text (index = -1)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBoxAvailableDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (comboBoxAvailableDevices.SelectedIndex < 0)
            {
                buttonConnect.IsEnabled = false;
            }
            else
            {
                buttonConnect.IsEnabled = true;
            }
        }

        /// <summary>
        /// Function automatically commands the textbox to scroll to the bottom
        /// after text has been written
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void outputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var grid = (Grid)VisualTreeHelper.GetChild(outputTextBox, 0);
            for (var i = 0; i <= VisualTreeHelper.GetChildrenCount(grid) - 1; i++)
            {
                object obj = VisualTreeHelper.GetChild(grid, i);
                if (!(obj is ScrollViewer)) continue;
                ((ScrollViewer)obj).ChangeView(0.0f, ((ScrollViewer)obj).ExtentHeight, 1.0f);
                break;
            }
        }

        /// <summary>
        /// Kicks off the device ping routine
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonPing_Click(object sender, RoutedEventArgs e)
        {
            disableAllAcquistionButtons();
            lastDeviceCommand = (byte)DeviceCommand.ConnectionPing;
            SendData_Click(DeviceCommand.ConnectionPing, 0, nullData);
        }

        /// <summary>
        /// Requests the firmware revision from the device
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonFirmwareRev_Click(object sender, RoutedEventArgs e)
        {
            disableAllAcquistionButtons();
            lastDeviceCommand = (byte)DeviceCommand.ReadFirmwareRevision;
            SendData_Click(DeviceCommand.ReadFirmwareRevision, 0, nullData);
        }

        /// <summary>
        /// Requests the FPGA revision from the device
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonFpgaRev_Click(object sender, RoutedEventArgs e)
        {
            disableAllAcquistionButtons();
            lastDeviceCommand = (byte)DeviceCommand.ReadFPGARevision;
            SendData_Click(DeviceCommand.ReadFPGARevision, 0, nullData);
        }

        /// <summary>
        /// Requests a line of spectrum from the device. 
        /// The returned data is handled differently in the ReadAsync function.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonAcquire_Click(object sender, RoutedEventArgs e)
        {
            disableAllAcquistionButtons();
            lastDeviceCommand = (byte)DeviceCommand.AcquireImage;
            SendData_Click(DeviceCommand.AcquireImage, 0, nullData);
        }
    }
}

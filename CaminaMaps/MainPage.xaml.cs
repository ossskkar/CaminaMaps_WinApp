using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using CaminaMaps.Resources;
using Microsoft.Phone.Shell;

/* Include Sensor Libraries */
using Microsoft.Devices.Sensors;
using Microsoft.Xna.Framework;
using System.Windows.Threading;

using System.Data.Linq;
using System.Data.Linq.Mapping;
using System.ComponentModel;
using System.Collections.ObjectModel;

using System.Windows.Media;

using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace CaminaMaps
{
    public partial class MainPage : PhoneApplicationPage
    {

        /* Declare variables */
        Compass compass;
        DispatcherTimer timer;

        /*Compass variables */
        double magneticHeading;
        double trueHeading;
        double headingAccuracy;
        Vector3 rawMagnetometerReading;
        bool isDataValid;
        bool calibrating = false;

        /* Accelerometer variables */
        Vector3 acceleration;
        Accelerometer accelerometer;

        /* Gyroscope variables */
        Gyroscope gyroscope;
        Vector3 currentRotationRate = Vector3.Zero;
        Vector3 cumulativeRotation = Vector3.Zero;

        DateTimeOffset lastUpdateTime = DateTimeOffset.MinValue;

        /* Sampling rate */
        int timeBetweenUpdates = 100; /* 4 samples per second, 1s = 1000ms */

        // Constructor
        public MainPage()
        {
            InitializeComponent();

            /* Check if accelerometer and compass sensors are available */
            if (!Accelerometer.IsSupported)
            {
                MessageBox.Show("Accelerometer sensor not supported", "Error", MessageBoxButton.OK);
                this.start.IsEnabled = false;
            }
            else if (!Compass.IsSupported)
            {
                MessageBox.Show("Compass sensor not supported", "Error", MessageBoxButton.OK);
                this.start.IsEnabled = false;
            }
            else if (!Gyroscope.IsSupported)
            {
                MessageBox.Show("Gyroscope sensor not supported", "Error", MessageBoxButton.OK);
                this.start.IsEnabled = false;
            }
            else
            {
                /* Create and activate timer */
                timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromMilliseconds(timeBetweenUpdates);
                timer.Tick += new EventHandler(timer_Tick);
            }
        }

        private void start_Click(object sender, RoutedEventArgs e)
        {
            // Start 
            if (this.start.Content.Equals("Start")) 
            {
                // Change label 
                this.start.Content = "Stop";

                // Set start time
                this.txtStartTime.Text = DateTime.Now.ToString("yyy/MM/dd-HH:mm:ss.ff");

                // Instantiate the compass 
                compass = new Compass();

                /* Set time between updates, that is the sampling rate */
                compass.TimeBetweenUpdates = TimeSpan.FromMilliseconds(timeBetweenUpdates);

                /* Display real sampling rate, the above specified sampling rate may not be supported */
                txtCompassTimeBetweenUpdates.Text = " " + compass.TimeBetweenUpdates.TotalMilliseconds + "ms";

                /* Obtain new readings from compass sensor */
                compass.CurrentValueChanged +=
                  new EventHandler<SensorReadingEventArgs<CompassReading>>(compass_CurrentValueChanged);

                /* Calibrate compass */
                compass.Calibrate +=
                   new EventHandler<CalibrationEventArgs>(compass_Calibrate);

                /* Initialize accelerometer */
                accelerometer = new Accelerometer();
                accelerometer.TimeBetweenUpdates = TimeSpan.FromMilliseconds(timeBetweenUpdates);
                accelerometer.CurrentValueChanged +=
                    new EventHandler<SensorReadingEventArgs<AccelerometerReading>>(accelerometer_CurrentValueChanged);

                /* Initialize gyroscope */
                gyroscope = new Gyroscope();
                gyroscope.TimeBetweenUpdates = TimeSpan.FromMilliseconds(timeBetweenUpdates);
                gyroscope.CurrentValueChanged +=
                    new EventHandler<SensorReadingEventArgs<GyroscopeReading>>(gyroscope_CurrentValueChanged);

                try
                {
                    /* Start accelerometer, compass and timer */
                    accelerometer.Start();
                    compass.Start();
                    gyroscope.Start();
                    timer.Start();
                }
                catch (InvalidOperationException)
                {
                    MessageBox.Show("Unable to start accelerometer/compass/gyroscope", "Error", MessageBoxButton.OK);
                }
            }
            else /* Stop */
            {
                /* Change label */
                this.start.Content = "Start";

                /* Add the file name of the readings to list */
                AddFileNameToList(this.txtStartTime.Text);

                /* Stops accelerometer, compass and timer */
                try
                {
                    accelerometer.Stop();
                    compass.Stop();
                    timer.Stop();
                }
                catch (InvalidOperationException)
                {
                    MessageBox.Show("Unable to stop accelerometer/compass", "Error", MessageBoxButton.OK);
                }
               
            }
        }

        void compass_CurrentValueChanged(object sender, SensorReadingEventArgs<CompassReading> e)
        {
            /* Read data from compass sensor */
            isDataValid = compass.IsDataValid;
            trueHeading = e.SensorReading.TrueHeading;
            magneticHeading = e.SensorReading.MagneticHeading;
            headingAccuracy = Math.Abs(e.SensorReading.HeadingAccuracy);
            rawMagnetometerReading = e.SensorReading.MagnetometerReading;
        }

        void timer_Tick(object sender, EventArgs e)
        {
            if (!calibrating)
            {
                if (isDataValid)
                {
                    /* update something*/
                }

                /* Write data to text file */
                WriteToFile(trueHeading, acceleration, currentRotationRate, cumulativeRotation, this.txtStartTime.Text);

                /* Updates compass data */
                txtCompass.Text = " " + (360 - trueHeading).ToString("0");

                /* Updates accelerometer data */
                txtAccelerationX.Text = acceleration.X.ToString("0.00");
                txtAccelerationY.Text = acceleration.Y.ToString("0.00");
                txtAccelerationZ.Text = acceleration.Z.ToString("0.00");

                /* Updates gyroscope data */
                txtGyroSpeedX.Text = currentRotationRate.X.ToString("0.000");
                txtGyroSpeedY.Text = currentRotationRate.Y.ToString("0.000");
                txtGyroSpeedZ.Text = currentRotationRate.Z.ToString("0.000");
                txtGyroCumulativeX.Text = MathHelper.ToDegrees(cumulativeRotation.X).ToString("0.00");
                txtGyroCumulativeY.Text = MathHelper.ToDegrees(cumulativeRotation.Y).ToString("0.00");
                txtGyroCumulativeZ.Text = MathHelper.ToDegrees(cumulativeRotation.Z).ToString("0.00");

                /* Change color of text box depending on the accuracy */
                if (headingAccuracy <= 10) /* good accuracy */
                {
                    txtCompass.Foreground = new SolidColorBrush(Colors.Green);
                }
                else /* bad accuracy */
                {
                    txtCompass.Foreground = new SolidColorBrush(Colors.Red);
                }
            }
            else
            {
                /* Check for accuracy */
                if (headingAccuracy <= 10) /* good accuracy */
                {
                    txtCompassCalibration.Foreground = new SolidColorBrush(Colors.Green);
                }
                else /* bad accuracy */
                {
                    txtCompassCalibration.Foreground = new SolidColorBrush(Colors.Red);
                }

                /* Display accuracy */
                //txtCompassCalibration.Text = " (" + headingAccuracy.ToString("0.0") + "): ";
            }
        }

        void compass_Calibrate(object sender, CalibrationEventArgs e)
        {
            //Dispatcher.BeginInvoke(() => { calibrationStackPanel.visibility = Visibility.Visible; });
            var msg = MessageBox.Show("The compass in your device needs to be calibrated. Hold the device in front of you and sweep it through a figure 8 pattern", "Calibrate your Device", MessageBoxButton.OK);
            if (msg == MessageBoxResult.OK)
            {

            }
        }

        void accelerometer_CurrentValueChanged(object sender, SensorReadingEventArgs<AccelerometerReading> e)
        {
            /* Read values to a 3 vector */
            acceleration = e.SensorReading.Acceleration;

            bool isCompassUsingNegativeZAxis = false;

            if (Math.Abs(acceleration.Z) < Math.Cos(Math.PI / 4) && (acceleration.Y < Math.Sin(7 * Math.PI / 4)))
            {
                isCompassUsingNegativeZAxis = true;
            }
        }

        void gyroscope_CurrentValueChanged(object sender, SensorReadingEventArgs<GyroscopeReading> e)
        {
            if (lastUpdateTime.Equals(DateTimeOffset.MinValue))
            {
                /* Last time updated */
                lastUpdateTime = e.SensorReading.Timestamp;
            }
            else
            {
                /* Get rotation rate */
                currentRotationRate = e.SensorReading.RotationRate;

                /* Calculate time span since last update */
                TimeSpan timeSinceLastUpdate = e.SensorReading.Timestamp - lastUpdateTime;

                /* Accumulate rotation */
                cumulativeRotation += currentRotationRate * (float)(timeSinceLastUpdate.TotalSeconds);

                /* Last time updated */
                lastUpdateTime = e.SensorReading.Timestamp;
            }
        }

        private async Task WriteToFile(double _trueHeading, Vector3 _acceleration,
            Vector3 _currentRotationRate, Vector3 _cumulativeRotation, string _timeStamp)
        {
            /* Create string of data */
            String data = _trueHeading.ToString("0") + ", "
                + _acceleration.X.ToString("0.00") + ", "
                + _acceleration.Y.ToString("0.00") + ", "
                + _acceleration.Z.ToString("0.00") + ", "
                + _currentRotationRate.X.ToString("0.00") + ", "
                + _currentRotationRate.Y.ToString("0.00") + ", "
                + _currentRotationRate.Z.ToString("0.00") + ", "
                + _cumulativeRotation.X.ToString("0.00") + ", "
                + _cumulativeRotation.Y.ToString("0.00") + ", "
                + _cumulativeRotation.Z.ToString("0.00") + ", "
                + _timeStamp;

            /* String data to byte */
            byte[] fileBytes = System.Text.Encoding.UTF8.GetBytes(data.ToCharArray());

            /* Get the local folder */
            StorageFolder local = Windows.Storage.ApplicationData.Current.LocalFolder;

            /* Create new folder or opens existing */
            var dataFolder = await local.CreateFolderAsync("Readings", CreationCollisionOption.OpenIfExists);

            string fileName = _timeStamp + ".txt";

            /* Create file */
            var file = await dataFolder.CreateFileAsync(fileName, CreationCollisionOption.OpenIfExists);

            /* Write data to file */
            using (var s = await file.OpenStreamForWriteAsync())
            {
                s.Write(fileBytes, 0, fileBytes.Length);
            }
        }

        private async Task AddFileNameToList(string _addFileName)
        {
            /* String data to byte */
            //byte[] fileBytes = System.Text.Encoding.UTF8.GetBytes(_addFileName.ToCharArray());

            /* Get the local folder */
            StorageFolder local = Windows.Storage.ApplicationData.Current.LocalFolder;

            /* Create new folder or opens existing */
            var dataFolder = await local.CreateFolderAsync("Readings", CreationCollisionOption.OpenIfExists);

            /* File name, here we store the name of the files for readings */
            string fileName = "./Readings/FileNameList.txt";

            /* Create file */
            //var file = await dataFolder.CreateFileAsync(fileName, CreationCollisionOption.OpenIfExists);

            /* Write data to file */
            //using (var s = await file.OpenStreamForWriteAsync())
            //using (var s = await file.OpenStreamForWriteAsync(FileMode.Append))
            //{
            //    s.Write(fileBytes, 0, fileBytes.Length);
            //}


            try
            {
                /* If file already exist then append */
                if (File.Exists(fileName))
                {
                    /* Open stream and write data */
                    using (StreamWriter s = new StreamWriter(File.Open(fileName, FileMode.Append, FileAccess.Write)))
                    {
                        s.WriteLine(_addFileName);
                    }
                }
                /* If file doesn't exist then create it */
                else
                {
                    /* Open stream and write data */
                    using (StreamWriter s = new StreamWriter(File.Open(fileName, FileMode.Create, FileAccess.Write)))
                    {
                        s.WriteLine(_addFileName);
                    }
                }
            }
            catch (IOException)
            {
            }
        }

        private void load_Click(object sender, RoutedEventArgs e)
        {
            ReadFile();
        }

        private async Task ReadFile()
        {
            /* Get the local folder */
            //StorageFolder local = Windows.Storage.ApplicationData.Current.LocalFolder;

            //if (local != null)
            //{
                /* Get the DataFolder folder */
                //var dataFolder = await local.GetFolderAsync("Readings");

                /* Get the file */
                //var file = await dataFolder.OpenStreamForReadAsync("FileNameList.txt");
                //var file = await OpenStreamForReadAsync("FileNameList.txt");

                /* Read the data */
                //using (StreamReader streamReader = new StreamReader(file))
                //{
                //    this.txtFileNameList.Text = streamReader.ReadToEnd();
                //    string temp = streamReader.ReadToEnd();
                //}

            //}
        }
    }
}











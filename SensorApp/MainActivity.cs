using Android.App;
using Android.Hardware;
using Android.OS;
using Android.Runtime;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;
using MikePhil.Charting.Charts;
using MikePhil.Charting.Data;
using MikePhil.Charting.Components;
using MikePhil.Charting.Interfaces.Datasets;
using Android.Graphics;
using System.Collections.Generic;
using Android.Content.PM;

namespace SensorApp
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true, ScreenOrientation = ScreenOrientation.Portrait)]
    public class MainActivity : AppCompatActivity, ISensorEventListener
    {
        SensorManager mSensorManager;
        TextView mAccelText, mCompassText;
        LineChart mAccelChart, mOrientationChart;
        static readonly object _syncLock = new object();
        const float gravity = 9.81f;
        LowPassFilter zOrientLP = new LowPassFilter();
        LowPassFilter xOrientLP = new LowPassFilter();
        LowPassFilter yOrientLP = new LowPassFilter();

        float[] lastAccelerometer = new float[3];
        float[] lastMagnetometer = new float[3];
        bool lastAccelerometerSet;
        bool lastMagnetometerSet;
        float[] r = new float[9];
        float[] orientation = new float[3];

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            Android.Support.V7.Widget.Toolbar toolbar = FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);

            mSensorManager = (SensorManager)GetSystemService(SensorService);

            mAccelText = FindViewById<TextView>(Resource.Id.accelerometer_text);
            mCompassText = FindViewById<TextView>(Resource.Id.compass_text);

            mAccelChart = FindViewById<LineChart>(Resource.Id.accelerometer_chart);
            PrepareLineChart(mAccelChart);
            mOrientationChart = FindViewById<LineChart>(Resource.Id.orientation_chart);
            PrepareLineChart(mOrientationChart);
        }

        // Set apearance of MPandroidChart here
        private void PrepareLineChart(LineChart mChart )
        {
            mChart.Description.Enabled = false;
            mChart.SetTouchEnabled(false);
            mChart.SetScaleEnabled(false);
            var legend = mChart.Legend;
            legend.Enabled = true;
            legend.TextSize = 12f;
            mChart.SetDrawGridBackground(false);
            mChart.AnimateY(1100);
            mChart.SetDrawBorders(false);
            mChart.SetHardwareAccelerationEnabled(true);
            var sets = new List<ILineDataSet>
            {
                CreateLineDataSet(Color.Magenta, "X"),
                CreateLineDataSet(Color.LimeGreen , "Y"),
                CreateLineDataSet(Color.DarkBlue , "Z")
            };
            LineData data = new LineData(sets);
            mChart.Data = data;
            XAxis xl = mChart.XAxis;
            xl.SetDrawGridLines(true);
            xl.SetAvoidFirstLastClipping(true);
            xl.Enabled = true;
            YAxis leftAxis = mChart.AxisLeft;
            leftAxis.SetDrawGridLines(false);
            leftAxis.SetDrawGridLines(true);
            YAxis rightAxis = mChart.AxisRight;
            rightAxis.Enabled = false;
        }

        public void OnSensorChanged(SensorEvent e)
        {
            lock (_syncLock)
            {
                if (e.Sensor.Type == SensorType.Accelerometer && !lastAccelerometerSet)
                {
                    float acc_x = e.Values[0] / gravity;
                    float acc_y = e.Values[1] / gravity;
                    float acc_z = e.Values[2] / gravity;
                    // Display text
                    mAccelText.Text = string.Format("Acceleration: x={0:f}, y={1:f}, z={2:f}", acc_x, acc_y, acc_z);
                    // Add to chart
                    AddAccelEntry(e.Values[0], e.Values[1], e.Values[2]);
                    // copy for orientation calculation
                    e.Values.CopyTo(lastAccelerometer, 0);
                    lastAccelerometerSet = true;
                }
                else if (e.Sensor.Type == SensorType.MagneticField && !lastMagnetometerSet)
                {
                    // copy for orientation calculation
                    e.Values.CopyTo(lastMagnetometer, 0);
                    lastMagnetometerSet = true;
                }

                if (lastAccelerometerSet && lastMagnetometerSet)
                {
                    if (e.Sensor.Type == SensorType.MagneticField || e.Sensor.Type == SensorType.Accelerometer)
                    {
                        SensorManager.GetRotationMatrix(r, null, lastAccelerometer, lastMagnetometer);
                        SensorManager.GetOrientation(r, orientation);
                        // low pass filter                    
                        xOrientLP.Add(orientation[1]);
                        yOrientLP.Add(orientation[2]);
                        zOrientLP.Add(orientation[0]);
                        var xOrient = xOrientLP.Average();
                        var yOrient = yOrientLP.Average();
                        var zOrient = zOrientLP.Average();
                        // convert in degrees
                        float xOrientDegrees = (float)((Java.Lang.Math.ToDegrees(xOrient) + 360.0) % 360.0);
                        float yOrientDegrees = (float)((Java.Lang.Math.ToDegrees(yOrient) + 360.0) % 360.0);
                        float zOrientDegrees = (float)((Java.Lang.Math.ToDegrees(zOrient) + 360.0) % 360.0);
                        // Display text
                        mCompassText.Text = string.Format("Orientation x={0:f}°,y={1:f}°,z={2:f}°", xOrientDegrees, yOrientDegrees, zOrientDegrees);
                        // Add to chart
                        AddOrientEntry(xOrientDegrees,yOrientDegrees, zOrientDegrees);
                        // set flags
                        lastMagnetometerSet = false;
                        lastAccelerometerSet = false;
                    }
                }
            }
        }

        private void AddAccelEntry(float valueX, float valueY, float valueZ)
        {
            LineData data = mAccelChart.LineData;
            if (data != null)
            {
                ILineDataSet set = (ILineDataSet)data.DataSets[0];
                data.AddEntry(new Entry(set.EntryCount, valueX), 0);
                data.AddEntry(new Entry(set.EntryCount, valueY), 1);
                data.AddEntry(new Entry(set.EntryCount, valueZ), 2);
                mAccelChart.NotifyDataSetChanged();
                // limit the number of visible entries
                mAccelChart.SetVisibleXRangeMaximum(100);
                // move to the latest entry
                mAccelChart.MoveViewToX(data.EntryCount);
            }
        }
        private void AddOrientEntry(float valueX, float valueY, float valueZ)
        {
            LineData data = mOrientationChart.LineData;
            if (data != null)
            {
                ILineDataSet set = (ILineDataSet)data.DataSets[0];
                data.AddEntry(new Entry(set.EntryCount, valueX), 0);
                data.AddEntry(new Entry(set.EntryCount, valueY), 1);
                data.AddEntry(new Entry(set.EntryCount, valueZ), 2);
                mOrientationChart.NotifyDataSetChanged();
                // limit the number of visible entries
                mOrientationChart.SetVisibleXRangeMaximum(100);
                // move to the latest entry
                mOrientationChart.MoveViewToX(data.EntryCount);
            }
        }

        private LineDataSet CreateLineDataSet(Color mcolor, string mLabel)
        {
            LineDataSet set = new LineDataSet(null, "Data")
            {
                AxisDependency = YAxis.AxisDependency.Left,
                LineWidth = 3f,
                Color = mcolor,
                HighlightEnabled = false,
                Label = mLabel
            };
            set.SetDrawValues(false);
            set.SetDrawCircles(false);
            set.SetMode(LineDataSet.Mode.CubicBezier);
            set.CubicIntensity = 0.2f;
            return set;
        }

        protected override void OnResume()
        {
            base.OnResume();
            var mAccel = mSensorManager.GetDefaultSensor(SensorType.Accelerometer);
            if ( mAccel != null)
            {
                mSensorManager.RegisterListener(this, mAccel, SensorDelay.Ui);
            }        
            var  mMagn = mSensorManager.GetDefaultSensor(SensorType.MagneticField);
            if (mMagn != null)
            {
                mSensorManager.RegisterListener(this, mMagn, SensorDelay.Ui);
            }
        }

        protected override void OnPause()
        {
            base.OnPause();
            mSensorManager.UnregisterListener(this);
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.menu_main, menu);
            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            int id = item.ItemId;
            if (id == Resource.Id.action_settings)
            {
                return true;
            }
            return base.OnOptionsItemSelected(item);
        }

        public void OnAccuracyChanged(Sensor sensor, [GeneratedEnum] SensorStatus accuracy)
        {
            // We don't want to do anything here.
        }
    }
}


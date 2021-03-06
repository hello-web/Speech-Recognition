﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Accord.Statistics.Models.Markov;
using SpeechRecognition.Audio;
using SpeechRecognition.VectorQuantization;
using Microsoft.Win32;

namespace SpeechRecognition.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {

        #region Fields

        private AppState _currentState;
        private HiddenMarkovModel[] _models;
        private Codebook _codebook;
        private MicrophoneSoundSignalReader _signal;
        private readonly Recorder _recorder = new Recorder();
        private SampleAggregator _aggregator;

        #endregion

        #region Instance

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        #endregion

        #region Methods

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var result = TrainResult.Load("SavedData", "model");
            if (result != null)
            {
                _models = result.Models;
                _codebook = result.Catalog;
            }
        }

        private void Train()
        {
            Dictionary<string, IList<ISoundSignalReader>> learningWordSignals = new Dictionary<string, IList<ISoundSignalReader>>();

            List<string> learningDirectories = new List<string>();
            foreach (var folder in ConfigurationSettings.LearningsFolders)
            {
                learningDirectories.AddRange(Directory.GetDirectories(folder));
            }

            foreach (var directory in learningDirectories.Where(item => !item.Contains("catalog")))
            {
                var word = new DirectoryInfo(directory).Name;
                learningWordSignals.Add(word, new List<ISoundSignalReader>());
                var wavFiles = Directory.GetFiles(directory).Select(item => new FileInfo(item)).Where(fItem => fItem.Extension.Contains("wav"));
                foreach (var file in wavFiles)
                {
                    learningWordSignals[word].Add(new WavSoundSignalReader(file.FullName));
                }
            }

            var catalogSignals = new List<ISoundSignalReader>();
            catalogSignals.AddRange(learningWordSignals.SelectMany(item => item.Value));

            var codeBook = CodeBookFactory.FromWaves(catalogSignals, EngineParameters.Default);

            var recognitionEngine = new DetectionEngine(codeBook);
            var result = recognitionEngine.Train(learningWordSignals);
            //result.Hmm.Save("HMModels.dat");
            _models = result.Models;
            _codebook = result.Catalog;

            result.Save("SavedData", "model");

        }

        private void LoadModel(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog();
            var ret = fileDialog.ShowDialog();
            if (ret.HasValue && ret.Value)
            {
                var folder = Path.GetDirectoryName(fileDialog.FileName);
                //var modelName= fileDialog.FileName.
                //var result = TrainResult.Load("SavedData", "model");
                //_classifier = result.Hmm;
                //_codebook = result.Catalog;
            }
        }

        private void StartTraining(object sender, RoutedEventArgs e)
        {
            btnStartStopRecord.IsEnabled = false;
            btnRecog.IsEnabled = false;
            btnTrain.IsEnabled = false;

            Train();

            btnStartStopRecord.IsEnabled = true;
            btnRecog.IsEnabled = true;
            btnTrain.IsEnabled = true;
        }

        private void StartStopRecognition(object sender, RoutedEventArgs e)
        {
            if (_currentState == AppState.Idle)
            {

                txtRecognizedText.Text = string.Empty;
                var recognitionEngine = new DetectionEngine(_codebook, _models);
                _signal = new MicrophoneSoundSignalReader();

                var length = (_signal.SampleRate / 1000.0) * EngineParameters.Default.StepSizeMiliseconds;
                if (_aggregator != null)
                {
                    _aggregator.SampleReady -= AggregatorSampleReady;
                }
                _aggregator = new SampleAggregator(Convert.ToInt32(length));

                _aggregator.SampleReady += AggregatorSampleReady;
                _signal.Start();
                Action action = () =>
                {
                    Thread.Sleep(3000);
                    recognitionEngine.RecognizeAsync(_signal, OnMessageReceived, _aggregator);
                };

                action.BeginInvoke(null, null);

                btnRecog.Content = "Stop Recognition";
                _currentState = AppState.Recognition;
            }
            else
            {
                btnRecog.Content = "Start Recognition";
                _signal.Close();
                _currentState = AppState.Idle;
                _aggregator.SampleReady -= AggregatorSampleReady;
                _aggregator = null;
            }

        }

        private void AggregatorSampleReady(object sender, SampleAggregator.SamplePointEventArgs e)
        {
            renderer.AddValue(e.Point);
        }

        private void StartStopRecording(object sender, RoutedEventArgs e)
        {
            if (_currentState != AppState.Recording && _currentState != AppState.Idle)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(txtWord.Text))
            {
                return;
            }

            if (_currentState == AppState.Idle) //"Start Recording"
            {
                var folderName = Path.Combine(ConfigurationSettings.RecordingFolder, txtWord.Text);
                if (!Directory.Exists(folderName))
                {
                    Directory.CreateDirectory(folderName);
                }

                folderName = Path.GetFullPath(folderName);
                var fileName = Guid.NewGuid() + ".wav";
                var fullFileName = Path.Combine(folderName, fileName);

                _recorder.Record(fullFileName);
                btnRecog.IsEnabled = false;
                btnTrain.IsEnabled = false;

                btnStartStopRecord.Content = "Stop Recording";
                _currentState = AppState.Recording;
            }
            else //"Stop Recording"
            {
                _recorder.Stop();
                btnStartStopRecord.Content = "Start Recording";
                btnRecog.IsEnabled = true;
                btnTrain.IsEnabled = true;
                _currentState = AppState.Idle;
            }
        }


        private void OnMessageReceived(string message)
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                txtRecognizedText.Text += message + Environment.NewLine;
                txtRecognizedText.ScrollToEnd();
            }), DispatcherPriority.DataBind);
        }

        #endregion

        public enum AppState
        {
            Idle = 0,
            Recording,
            Training,
            Recognition

        }
    }


}

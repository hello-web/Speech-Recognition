﻿using System.Collections.Generic;
using System.Linq;
using SpeechRecognition.Audio;
using SpeechRecognition.VectorQuantization;

namespace SpeechRecognition
{
    public class CodeBookFactory
    {

        public static Codebook FromWaves(IList<SoundSignalReader> sounds, EngineParameters parameters, int codeBookSize = 256)
        {
            var featureUtility = new FeatureUtility(parameters, null);
            var features = new List<double[][]>();            
            for (var signalIndex = 0; signalIndex < sounds.Count; signalIndex++)
            {
                var signal = sounds[signalIndex];
                var items = featureUtility.ExtractFeatures(signal).Select(item => item.ToArray());

                features.AddRange(items);
            }            


            var codeBook = new Codebook(features.SelectMany(item => item).Select(item => new Point(item)).ToArray(), codeBookSize);

            return codeBook;
        }
    }
}

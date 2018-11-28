using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.ML.Runtime.Data;
using Microsoft.ML.Runtime.Api;
using Microsoft.ML.Runtime.TimeSeriesProcessing;
using Microsoft.ML.Core.Data;
using Microsoft.ML.TimeSeries;
using System.IO;
using Microsoft.ML.Data;

namespace Microsoft.ML.Samples.Dynamic
{
    public partial class TransformSamples
    {
        class ChangePointPrediction
        {
            [VectorType(4)]
            public double[] Prediction { get; set; }
        }

        class IidChangePointData
        {
            public float Value;

            public IidChangePointData(float value)
            {
                Value = value;
            }
        }

        // This example creates a time series (list of Data with the i-th element corresponding to the i-th time slot). 
        // IidChangePointDetector is applied then to identify points where data distribution changed.
        public static void IidChangePointDetectorTransform()
        {
            // Create a new ML context, for ML.NET operations. It can be used for exception tracking and logging, 
            // as well as the source of randomness.
            var ml = new MLContext();

            // Generate sample series data with a change
            const int size = 16;
            var data = new List<IidChangePointData>(size);
            for (int i = 0; i < size / 2; i++)
                data.Add(new IidChangePointData(5));
            // This is a change point
            for (int i = 0; i < size / 2; i++)
                data.Add(new IidChangePointData(7));

            // Convert data to IDataView.
            var dataView = ml.CreateStreamingDataView(data);

            // Setup IidSpikeDetector arguments
            string outputColumnName = "Prediction";
            string inputColumnName = "Value";
            var args = new IidChangePointDetector.Arguments()
            {
                Source = inputColumnName,
                Name = outputColumnName,
                Confidence = 95,                // The confidence for spike detection in the range [0, 100]
                ChangeHistoryLength = size / 4, // The length of the sliding window on p-values for computing the martingale score. 
            };

            // The transformed data.
            var transformedData = new IidChangePointEstimator(ml, args).Fit(dataView).Transform(dataView);

            // Getting the data of the newly created column as an IEnumerable of ChangePointPrediction.
            var predictionColumn = transformedData.AsEnumerable<ChangePointPrediction>(ml, reuseRowObject: false);

            Console.WriteLine($"{outputColumnName} column obtained post-transformation.");
            Console.WriteLine("Data\tAlert\tScore\tP-Value\tMartingale value");
            int k = 0;
            foreach (var prediction in predictionColumn)
                Console.WriteLine("{0}\t{1}\t{2:0.00}\t{3:0.00}\t{4:0.00}", data[k++].Value, prediction.Prediction[0], prediction.Prediction[1], prediction.Prediction[2], prediction.Prediction[3]);
            Console.WriteLine("");

            // Prediction column obtained post-transformation.
            // Data Alert      Score   P-Value Martingale value
            // 5       0       5.00    0.50    0.00
            // 5       0       5.00    0.50    0.00
            // 5       0       5.00    0.50    0.00
            // 5       0       5.00    0.50    0.00
            // 5       0       5.00    0.50    0.00
            // 5       0       5.00    0.50    0.00
            // 5       0       5.00    0.50    0.00
            // 5       0       5.00    0.50    0.00
            // 7       1       7.00    0.00    10298.67   <-- alert is on, predicted changepoint
            // 7       0       7.00    0.13    33950.16
            // 7       0       7.00    0.26    60866.34
            // 7       0       7.00    0.38    78362.04
            // 7       0       7.00    0.50    0.01
            // 7       0       7.00    0.50    0.00
            // 7       0       7.00    0.50    0.00
            // 7       0       7.00    0.50    0.00
        }

        // This example creates a time series (list of Data with the i-th element corresponding to the i-th time slot). 
        // IidChangePointDetector is applied then to identify points where data distribution changed using time series 
        // prediction engine. The engine is checkpointed and then loaded back from disk into memory and used for prediction.
        public static void IidChangePointDetectorPrediction()
        {
            // Create a new ML context, for ML.NET operations. It can be used for exception tracking and logging, 
            // as well as the source of randomness.
            var ml = new MLContext();

            // Generate sample series data with a change
            const int size = 16;
            var data = new List<IidChangePointData>(size);
            for (int i = 0; i < size / 2; i++)
                data.Add(new IidChangePointData(5));
            // This is a change point
            for (int i = 0; i < size / 2; i++)
                data.Add(new IidChangePointData(7));

            // Convert data to IDataView.
            var dataView = ml.CreateStreamingDataView(data);

            // Setup IidSpikeDetector arguments
            string outputColumnName = "Prediction";
            string inputColumnName = "Value";
            var args = new IidChangePointDetector.Arguments()
            {
                Source = inputColumnName,
                Name = outputColumnName,
                Confidence = 95,                // The confidence for spike detection in the range [0, 100]
                ChangeHistoryLength = size / 4, // The length of the sliding window on p-values for computing the martingale score. 
            };

            // Time Series model.
            ITransformer model = new IidChangePointEstimator(ml, args).Fit(dataView);

            // Create a time series prediction engine from the model.
            var engine = model.CreateTimeSeriesPredictionFunction<IidChangePointData, ChangePointPrediction>(ml);
            for(int index = 0; index < 8; index++)
            {
                // Anomaly change point detection.
                var prediction = engine.Predict(new IidChangePointData(5));
                Console.WriteLine("{0}\t{1}\t{2:0.00}\t{3:0.00}\t{4:0.00}", 5, prediction.Prediction[0], 
                    prediction.Prediction[1], prediction.Prediction[2], prediction.Prediction[3]);
            }

            // Change point
            var changePointPrediction = engine.Predict(new IidChangePointData(7));
            Console.WriteLine("{0}\t{1}\t{2:0.00}\t{3:0.00}\t{4:0.00}", 7, changePointPrediction.Prediction[0],
                changePointPrediction.Prediction[1], changePointPrediction.Prediction[2], changePointPrediction.Prediction[3]);

            // Checkpoint the model.
            var modelPath = "temp.zip";
            engine.CheckPoint(ml, modelPath);

            // Load the model.
            using (var file = File.OpenRead(modelPath))
                model = TransformerChain.LoadFrom(ml, file);

            // Create a time series prediction engine from the model.
            engine = model.CreateTimeSeriesPredictionFunction<IidChangePointData, ChangePointPrediction>(ml);
            for (int index = 0; index < 8; index++)
            {
                // Anomaly change point detection.
                var prediction = engine.Predict(new IidChangePointData(7));
                Console.WriteLine("{0}\t{1}\t{2:0.00}\t{3:0.00}\t{4:0.00}", 7, prediction.Prediction[0],
                    prediction.Prediction[1], prediction.Prediction[2], prediction.Prediction[3]);
            }

            // Data Alert      Score   P-Value Martingale value
            // 5       0       5.00    0.50    0.00
            // 5       0       5.00    0.50    0.00
            // 5       0       5.00    0.50    0.00
            // 5       0       5.00    0.50    0.00
            // 5       0       5.00    0.50    0.00
            // 5       0       5.00    0.50    0.00
            // 5       0       5.00    0.50    0.00
            // 5       0       5.00    0.50    0.00
            // 7       1       7.00    0.00    10298.67   <-- alert is on, predicted changepoint (and model is checkpointed).
            // 7       0       7.00    0.13    33950.16   <-- model loaded back from disk and prediction is made.
            // 7       0       7.00    0.26    60866.34
            // 7       0       7.00    0.38    78362.04
            // 7       0       7.00    0.50    0.01
            // 7       0       7.00    0.50    0.00
            // 7       0       7.00    0.50    0.00
            // 7       0       7.00    0.50    0.00
        }
    }
}

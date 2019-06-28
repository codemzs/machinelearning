﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.CommandLine;
using Microsoft.ML.Data;
using Microsoft.ML.Runtime;
using Microsoft.ML.Transforms.TimeSeries;

[assembly: LoadableClass(SsaForecasting.Summary, typeof(IDataTransform), typeof(SsaForecasting), typeof(SsaForecasting.Options), typeof(SignatureDataTransform),
    SsaForecasting.UserName, SsaForecasting.LoaderSignature, SsaForecasting.ShortName)]

[assembly: LoadableClass(SsaForecasting.Summary, typeof(IDataTransform), typeof(SsaForecasting), null, typeof(SignatureLoadDataTransform),
    SsaForecasting.UserName, SsaForecasting.LoaderSignature)]

[assembly: LoadableClass(SsaForecasting.Summary, typeof(SsaForecasting), null, typeof(SignatureLoadModel),
    SsaForecasting.UserName, SsaForecasting.LoaderSignature)]

[assembly: LoadableClass(typeof(IRowMapper), typeof(SsaForecasting), null, typeof(SignatureLoadRowMapper),
   SsaForecasting.UserName, SsaForecasting.LoaderSignature)]

namespace Microsoft.ML.Transforms.TimeSeries
{
    /// <summary>
    /// <see cref="ITransformer"/> resulting from fitting a <see cref="SsaForecastingEstimator"/>.
    /// </summary>
    public sealed class SsaForecasting : SsaForecastingBaseWrapper, IStatefulTransformer, IForecastTransformer
    {
        internal const string Summary = "This transform forecasts using Singular Spectrum Analysis (SSA).";
        internal const string LoaderSignature = "SsaForecasting";
        internal const string UserName = "SSA Forecasting";
        internal const string ShortName = "ssafcst";

        internal sealed class Options : TransformInputBase
        {
            [Argument(ArgumentType.Required, HelpText = "The name of the source column.", ShortName = "src", SortOrder = 1, Purpose = SpecialPurpose.ColumnName)]
            public string Source;

            [Argument(ArgumentType.Required, HelpText = "The name of the new column.", SortOrder = 2)]
            public string Name;

            [Argument(ArgumentType.AtMostOnce, HelpText = "The name of the minimum confidence interval column.", ShortName = "cnfminname", SortOrder = 3)]
            public string ForecastingConfidenceIntervalMinOutputColumnName;

            [Argument(ArgumentType.AtMostOnce, HelpText = "The name of the maximum confidence interval column.", ShortName = "cnfmaxnname", SortOrder = 3)]
            public string ForecastingConfidenceIntervalMaxOutputColumnName;

            [Argument(ArgumentType.AtMostOnce, HelpText = "The discount factor in [0,1] used for online updates.", ShortName = "disc", SortOrder = 5)]
            public float DiscountFactor = 1;

            [Argument(ArgumentType.AtMostOnce, HelpText = "The flag determing whether the model is adaptive", ShortName = "adp", SortOrder = 6)]
            public bool IsAdaptive = false;

            [Argument(ArgumentType.Required, HelpText = "The length of the window on the series for building the trajectory matrix (parameter L).", SortOrder = 2)]
            public int WindowSize;

            [Argument(ArgumentType.AtMostOnce, HelpText = "The rank selection method.", SortOrder = 3)]
            public RankSelectionMethod RankSelectionMethod = RankSelectionMethod.Exact;

            [Argument(ArgumentType.AtMostOnce, HelpText = "The desired rank of the subspace used for SSA projection (parameter r). This parameter should be in the range in [1, windowSize]. " +
                "If set to null, the rank is automatically determined based on prediction error minimization.", SortOrder = 3)]
            public int? Rank = null;

            [Argument(ArgumentType.AtMostOnce, HelpText = "The maximum rank considered during the rank selection process. If not provided (i.e. set to null), it is set to windowSize - 1.", SortOrder = 3)]
            public int? MaxRank = null;

            [Argument(ArgumentType.AtMostOnce, HelpText = "The flag determining whether the model should be stabilized.", SortOrder = 3)]
            public bool ShouldStablize = true;

            [Argument(ArgumentType.AtMostOnce, HelpText = "The flag determining whether the meta information for the model needs to be maintained.", SortOrder = 3)]
            public bool ShouldMaintainInfo = false;

            [Argument(ArgumentType.AtMostOnce, HelpText = "The maximum growth on the exponential trend.", SortOrder = 3)]
            public GrowthRatio? MaxGrowth = null;

            [Argument(ArgumentType.Required, HelpText = "The length of series that is kept in buffer for modeling (parameter N).", SortOrder = 2)]
            public int SeriesLength;

            [Argument(ArgumentType.Required, HelpText = "The length of series from the begining used for training.", SortOrder = 2)]
            public int TrainSize;

            [Argument(ArgumentType.Required, HelpText = "The number of values to forecast.", SortOrder = 2)]
            public int Horizon;

            [Argument(ArgumentType.AtMostOnce, HelpText = "The confidence level in [0, 1) for forecasting.", SortOrder = 2)]
            public float ConfidenceLevel = 0.95f;
        }

        private sealed class BaseArguments : SsaForecastingOptions
        {
            public BaseArguments(Options options)
            {
                Source = options.Source;
                Name = options.Name;
                ForecastingConfidenceIntervalMinOutputColumnName = options.ForecastingConfidenceIntervalMinOutputColumnName;
                ForecastingConfidenceIntervalMaxOutputColumnName = options.ForecastingConfidenceIntervalMaxOutputColumnName;
                WindowSize = options.WindowSize;
                DiscountFactor = options.DiscountFactor;
                IsAdaptive = options.IsAdaptive;
                RankSelectionMethod = options.RankSelectionMethod;
                Rank = options.Rank;
                ShouldStablize = options.ShouldStablize;
                MaxGrowth = options.MaxGrowth;
                SeriesLength = options.SeriesLength;
                TrainSize = options.TrainSize;
                Horizon = options.Horizon;
                ConfidenceLevel = options.ConfidenceLevel;
            }
        }

        private static VersionInfo GetVersionInfo()
        {
            return new VersionInfo(
                modelSignature: "FRCSTRNS",
                verWrittenCur: 0x00010001, // Initial
                verReadableCur: 0x00010001,
                verWeCanReadBack: 0x00010001,
                loaderSignature: LoaderSignature,
                loaderAssemblyName: typeof(SsaForecasting).Assembly.FullName);
        }

        internal SsaForecasting(IHostEnvironment env, Options options, IDataView input)
            : base(new BaseArguments(options), LoaderSignature, env)
        {
            InternalTransform.Model.Train(new RoleMappedData(input, null, InternalTransform.InputColumnName));
        }

        // Factory method for SignatureDataTransform.
        private static IDataTransform Create(IHostEnvironment env, Options options, IDataView input)
        {
            Contracts.CheckValue(env, nameof(env));
            env.CheckValue(options, nameof(options));
            env.CheckValue(input, nameof(input));

            return new SsaForecasting(env, options, input).MakeDataTransform(input);
        }

        internal SsaForecasting(IHostEnvironment env, Options options)
            : base(new BaseArguments(options), LoaderSignature, env)
        {
            // This constructor is empty.
        }

        // Factory method for SignatureLoadDataTransform.
        private static IDataTransform Create(IHostEnvironment env, ModelLoadContext ctx, IDataView input)
        {
            Contracts.CheckValue(env, nameof(env));
            env.CheckValue(ctx, nameof(ctx));
            env.CheckValue(input, nameof(input));

            return new SsaForecasting(env, ctx).MakeDataTransform(input);
        }

        IStatefulTransformer IStatefulTransformer.Clone()
        {
            var clone = (SsaForecasting)MemberwiseClone();
            clone.InternalTransform.Model = clone.InternalTransform.Model.Clone();
            clone.InternalTransform.StateRef = (SsaForecastingBase.State)clone.InternalTransform.StateRef.Clone();
            clone.InternalTransform.StateRef.InitState(clone.InternalTransform, InternalTransform.Host);
            return clone;
        }

        // Factory method for SignatureLoadModel.
        private static SsaForecasting Create(IHostEnvironment env, ModelLoadContext ctx)
        {
            Contracts.CheckValue(env, nameof(env));
            env.CheckValue(ctx, nameof(ctx));
            ctx.CheckAtModel(GetVersionInfo());

            return new SsaForecasting(env, ctx);
        }

        internal SsaForecasting(IHostEnvironment env, ModelLoadContext ctx)
            : base(env, ctx, LoaderSignature)
        {
            // *** Binary format ***
            // <base>
            InternalTransform.Host.CheckDecode(InternalTransform.IsAdaptive == false);
        }

        private protected override void SaveModel(ModelSaveContext ctx)
        {
            InternalTransform.Host.CheckValue(ctx, nameof(ctx));
            ctx.CheckAtModel();
            ctx.SetVersionInfo(GetVersionInfo());

            InternalTransform.Host.Assert(InternalTransform.IsAdaptive == false);

            // *** Binary format ***
            // <base>

            base.SaveModel(ctx);
        }

        // Factory method for SignatureLoadRowMapper.
        private static IRowMapper Create(IHostEnvironment env, ModelLoadContext ctx, DataViewSchema inputSchema)
            => Create(env, ctx).MakeRowMapper(inputSchema);
    }

    /// <summary>
    /// Detect spikes in time series using Singular Spectrum Analysis.
    /// </summary>
    /// <remarks>
    /// <format type="text/markdown"><![CDATA[
    /// To create this estimator, use [ForecastBySsa](xref:Microsoft.ML.TimeSeriesCatalog.ForecastBySsa(Microsoft.ML.TransformsCatalog,System.String,System.String,System.Int32,System.Int32,System.Int32,System.Int32,System.Boolean, System.Single, Microsoft.ML.Transforms.TimeSeries.AnomalySide,Microsoft.ML.Transforms.TimeSeries.ErrorFunction))
    ///
    /// [!include[io](~/../docs/samples/docs/api-reference/io-time-series-spike.md)]
    ///
    /// ###  Estimator Characteristics
    /// |  |  |
    /// | -- | -- |
    /// | Does this estimator need to look at the data to train its parameters? | Yes |
    /// | Input column data type | <xref:System.Single> |
    /// | Output column data type | Vector of <xref:System.Single> |
    ///
    /// [!include[io](~/../docs/samples/docs/api-reference/time-series-props.md)]
    ///
    /// [!include[io](~/../docs/samples/docs/api-reference/time-series-ssa.md)]
    ///
    /// Check the See Also section for links to usage examples.
    /// ]]>
    /// </format>
    /// </remarks>
    public sealed class SsaForecastingEstimator : IEstimator<SsaForecasting>
    {
        private readonly IHost _host;
        private readonly SsaForecasting.Options _options;

        /// <summary>
        /// Create a new instance of <see cref="SsaForecastingEstimator"/>
        /// </summary>
        /// <param name="env">Host Environment.</param>
        /// <param name="outputColumnName">Name of the column resulting from the transformation of <paramref name="inputColumnName"/>.</param>
        /// <param name="inputColumnName">Name of column to transform. If set to <see langword="null"/>, the value of the <paramref name="outputColumnName"/> will be used as source.
        /// The vector contains Alert, Raw Score, P-Value as first three values.</param>
        /// <param name="windowSize">The length of the window on the series for building the trajectory matrix (parameter L).</param>
        /// <param name="seriesLength">The length of series that is kept in buffer for modeling (parameter N).</param>
        /// <param name="trainSize">The length of series from the begining used for training.</param>
        /// <param name="horizon">The number of values to forecast.</param>
        /// <param name="isAdaptive">The flag determing whether the model is adaptive.</param>
        /// <param name="discountFactor">The discount factor in [0,1] used for online updates.</param>
        /// <param name="rankSelectionMethod">The rank selection method.</param>
        /// <param name="rank">The desired rank of the subspace used for SSA projection (parameter r). This parameter should be in the range in [1, windowSize].
        /// If set to null, the rank is automatically determined based on prediction error minimization.</param>
        /// <param name="maxRank">The maximum rank considered during the rank selection process. If not provided (i.e. set to null), it is set to windowSize - 1.</param>
        /// <param name="shouldStablize">The flag determining whether the model should be stabilized.</param>
        /// <param name="shouldMaintainInfo">The flag determining whether the meta information for the model needs to be maintained.</param>
        /// <param name="maxGrowth">The maximum growth on the exponential trend.</param>
        /// <param name="forecastingConfidenceIntervalMinOutputColumnName">The name of the minimum confidence interval column. If not specified then confidence intervals will not be calculated.</param>
        /// <param name="forecastingConfidenceIntervalMaxOutputColumnName">The name of the maximum confidence interval column. If not specified then confidence intervals will not be calculated.</param>
        /// <param name="confidenceLevel"></param>
        internal SsaForecastingEstimator(IHostEnvironment env,
            string outputColumnName,
            string inputColumnName,
            int windowSize,
            int seriesLength,
            int trainSize,
            int horizon,
            bool isAdaptive = false,
            float discountFactor = 1,
            RankSelectionMethod rankSelectionMethod = RankSelectionMethod.Exact,
            int? rank = null,
            int? maxRank = null,
            bool shouldStablize = true,
            bool shouldMaintainInfo = false,
            GrowthRatio? maxGrowth = null,
            string forecastingConfidenceIntervalMinOutputColumnName = null,
            string forecastingConfidenceIntervalMaxOutputColumnName = null,
            float confidenceLevel = 0.95f)
            : this(env, new SsaForecasting.Options
            {
                Source = inputColumnName ?? outputColumnName,
                Name = outputColumnName,
                DiscountFactor = discountFactor,
                IsAdaptive = isAdaptive,
                WindowSize = windowSize,
                RankSelectionMethod = rankSelectionMethod,
                Rank = rank,
                MaxRank = maxRank,
                ShouldStablize = shouldStablize,
                ShouldMaintainInfo = shouldMaintainInfo,
                MaxGrowth = maxGrowth,
                ConfidenceLevel = confidenceLevel,
                ForecastingConfidenceIntervalMinOutputColumnName = forecastingConfidenceIntervalMinOutputColumnName,
                ForecastingConfidenceIntervalMaxOutputColumnName = forecastingConfidenceIntervalMaxOutputColumnName
            })
        {
        }

        internal SsaForecastingEstimator(IHostEnvironment env, SsaForecasting.Options options)
        {
            Contracts.CheckValue(env, nameof(env));
            _host = env.Register(nameof(SsaForecastingEstimator));

            _host.CheckNonEmpty(options.Name, nameof(options.Name));
            _host.CheckNonEmpty(options.Source, nameof(options.Source));

            _options = options;
        }

        /// <summary>
        /// Train and return a transformer.
        /// </summary>
        public SsaForecasting Fit(IDataView input)
        {
            _host.CheckValue(input, nameof(input));
            return new SsaForecasting(_host, _options, input);
        }

        /// <summary>
        /// Schema propagation for transformers.
        /// Returns the output schema of the data, if the input schema is like the one provided.
        /// </summary>
        public SchemaShape GetOutputSchema(SchemaShape inputSchema)
        {
            _host.CheckValue(inputSchema, nameof(inputSchema));

            if (!inputSchema.TryFindColumn(_options.Source, out var col))
                throw _host.ExceptSchemaMismatch(nameof(inputSchema), "input", _options.Source);
            if (col.ItemType != NumberDataViewType.Single)
                throw _host.ExceptSchemaMismatch(nameof(inputSchema), "input", _options.Source, "Single", col.GetTypeString());

            var metadata = new List<SchemaShape.Column>() {
                new SchemaShape.Column(AnnotationUtils.Kinds.SlotNames, SchemaShape.Column.VectorKind.Vector, TextDataViewType.Instance, false)
            };

            var resultDic = inputSchema.ToDictionary(x => x.Name);
            resultDic[_options.Name] = new SchemaShape.Column(
                _options.Name, SchemaShape.Column.VectorKind.Vector, NumberDataViewType.Single, false, new SchemaShape(metadata));

            if (!string.IsNullOrEmpty(_options.ForecastingConfidenceIntervalMaxOutputColumnName))
            {
                resultDic[_options.ForecastingConfidenceIntervalMinOutputColumnName] = new SchemaShape.Column(
                    _options.ForecastingConfidenceIntervalMinOutputColumnName, SchemaShape.Column.VectorKind.Vector,
                    NumberDataViewType.Single, false, new SchemaShape(metadata));

                resultDic[_options.ForecastingConfidenceIntervalMaxOutputColumnName] = new SchemaShape.Column(
                    _options.ForecastingConfidenceIntervalMaxOutputColumnName, SchemaShape.Column.VectorKind.Vector,
                    NumberDataViewType.Single, false, new SchemaShape(metadata));
            }

            return new SchemaShape(resultDic.Values);
        }
    }
}

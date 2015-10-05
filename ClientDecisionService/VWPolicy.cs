﻿using VW;
using VW.Interfaces;
using MultiWorldTesting;
using System;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Collections.Generic;

namespace ClientDecisionService
{
    /// <summary>
    /// Represent an updatable <see cref="IPolicy<TContext>"/> object which can consume different VowpalWabbit 
    /// models to predict a list of actions from an object of specified <see cref="TContext"/> type.
    /// </summary>
    /// <typeparam name="TContext">The type of the context.</typeparam>
    public class VWPolicy<TContext, TActionDependentFeature> : IPolicy<TContext>, IDisposable
    {
        /// <summary>
        /// Constructor using an optional model file.
        /// </summary>
        /// <param name="vwModelFile">Optional; the VowpalWabbit model file to load from.</param>
        public VWPolicy(Func<TContext, IReadOnlyCollection<TActionDependentFeature>> getContextFeaturesFunc, string vwModelFile = null)
        {
            this.getContextFeaturesFunc = getContextFeaturesFunc;
            if (vwModelFile != null)
            {
                this.ModelUpdate(vwModelFile);
            }
        }

        /// <summary>
        /// Constructor using a memory stream.
        /// </summary>
        /// <param name="vwModelStream">The VW model memory stream.</param>
        public VWPolicy(Func<TContext, IReadOnlyCollection<TActionDependentFeature>> getContextFeaturesFunc, Stream vwModelStream)
        {
            this.getContextFeaturesFunc = getContextFeaturesFunc;
            this.ModelUpdate(vwModelStream);
        }

        /// <summary>
        /// Scores the model against the specified context and returns a list of actions (1-based index).
        /// </summary>
        /// <param name="context">The context object.</param>
        /// <returns>List of predicted actions.</returns>
        public uint[] ChooseAction(TContext context)
        {
            if (vwPool == null)
            {
                // TODO: return null or empty array, check with Stephen
            }
            using (var vw = vwPool.GetOrCreate())
            {
                IReadOnlyCollection<TActionDependentFeature> features = this.getContextFeaturesFunc(context);

                // return indices
                Tuple<int, TActionDependentFeature>[] vwMultilabelPredictions = vw.Value.Predict(context, features);

                // VW multi-label predictions are 0-based
                return vwMultilabelPredictions.Select(p => (uint)(p.Item1 + 1)).ToArray();
            }
        }

        /// <summary>
        /// Update VW model from file.
        /// </summary>
        /// <param name="modelFile">The model file to load.</param>
        /// <returns>true if the update was successful; otherwise, false.</returns>
        public bool ModelUpdate(string modelFile)
        {
            return ModelUpdate(() => { return new VowpalWabbitModel(new VowpalWabbitSettings(string.Format("--quiet -t -i {0}", modelFile), maxExampleCacheSize: 1024)); });
        }

        /// <summary>
        /// Update VW model from stream.
        /// </summary>
        /// <param name="modelStream">The model stream to load from.</param>
        /// <returns>true if the update was successful; otherwise, false.</returns>
        public bool ModelUpdate(Stream modelStream)
        {
            return ModelUpdate(() => new VowpalWabbitModel(new VowpalWabbitSettings("--quiet -t", modelStream: modelStream, maxExampleCacheSize: 1024)));
        }

        /// <summary>
        /// Update VW model using a generic method which loads the model.
        /// </summary>
        /// <param name="loadModelFunc">The generic method to load the model.</param>
        /// <returns>true if the update was successful; otherwise, false.</returns>
        public bool ModelUpdate(Func<VowpalWabbitModel> loadModelFunc)
        {
            VowpalWabbitModel vwModel = loadModelFunc();

            if (this.vwPool == null)
            {
                this.vwPool = new VowpalWabbitThreadedPrediction<TContext, TActionDependentFeature>(vwModel);
            }

            return true;
        }

        /// <summary>
        /// Dispose the object and clean up any resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose the object.
        /// </summary>
        /// <param name="disposing">Whether the object is disposing resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.vwPool != null)
                {
                    this.vwPool.Dispose();
                    this.vwPool = null;
                }
            }
        }

        private VowpalWabbitThreadedPrediction<TContext, TActionDependentFeature> vwPool;
        private Func<TContext, IReadOnlyCollection<TActionDependentFeature>> getContextFeaturesFunc;
    }
}

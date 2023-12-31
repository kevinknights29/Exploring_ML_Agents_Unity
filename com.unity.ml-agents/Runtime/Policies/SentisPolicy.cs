using Unity.Sentis;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Inference;
using Unity.MLAgents.Sensors;

namespace Unity.MLAgents.Policies
{
    /// <summary>
    /// Where to perform inference.
    /// </summary>
    public enum InferenceDevice
    {
        /// <summary>
        /// Default inference. This is currently the same as Burst, but may change in the future.
        /// </summary>
        Default = 0,

        /// <summary>
        /// GPU inference with the Compute Shader backend. Corresponds to WorkerFactory.Type.ComputeShader in Sentis.
        /// </summary>
        ComputeShader = 1,

        /// <summary>
        /// CPU inference using Burst. Corresponds to WorkerFactory.Type.CSharpBurst in Sentis.
        /// </summary>
        Burst = 2,

        /// <summary>
        /// GPU inference with the Pixel Shader backend. Corresponds to in WorkerFactory.Type.PixelShader Sentis.
        /// Burst is recommended instead; this is kept for legacy compatibility.
        /// </summary>
        PixelShader = 3,
    }

    /// <summary>
    /// The Sentis Policy uses a Sentis Model to make decisions at
    /// every step. It uses a ModelRunner that is shared across all
    /// Sentis Policies that use the same model and inference devices.
    /// </summary>
    internal class SentisPolicy : IPolicy
    {
        protected ModelRunner m_ModelRunner;
        ActionBuffers m_LastActionBuffer;

        int m_AgentId;
        /// <summary>
        /// Inference only: set to true if the action selection from model should be
        /// deterministic.
        /// </summary>
        bool m_DeterministicInference;

        /// <summary>
        /// Sensor shapes for the associated Agents. All Agents must have the same shapes for their Sensors.
        /// </summary>
        List<int[]> m_SensorShapes;
        ActionSpec m_ActionSpec;

        private string m_BehaviorName;

        /// <summary>
        /// List of actuators, only used for analytics
        /// </summary>
        private IList<IActuator> m_Actuators;

        /// <summary>
        /// Whether or not we've tried to send analytics for this model. We only ever try to send once per policy,
        /// and do additional deduplication in the analytics code.
        /// </summary>
        private bool m_AnalyticsSent;

        /// <summary>
        /// Instantiate a SentisPolicy with the necessary objects for it to run.
        /// </summary>
        /// <param name="actionSpec">The action spec of the behavior.</param>
        /// <param name="actuators">The actuators used for this behavior.</param>
        /// <param name="model">The Neural Network to use.</param>
        /// <param name="inferenceDevice">Which device Sentis will run on.</param>
        /// <param name="behaviorName">The name of the behavior.</param>
        /// <param name="deterministicInference"> Inference only: set to true if the action selection from model should be
        /// deterministic. </param>
        public SentisPolicy(
            ActionSpec actionSpec,
            IList<IActuator> actuators,
            ModelAsset model,
            InferenceDevice inferenceDevice,
            string behaviorName,
            bool deterministicInference = false
        )
        {
            var modelRunner = Academy.Instance.GetOrCreateModelRunner(model, actionSpec, inferenceDevice, deterministicInference);
            m_ModelRunner = modelRunner;
            m_BehaviorName = behaviorName;
            m_ActionSpec = actionSpec;
            m_Actuators = actuators;
            m_DeterministicInference = deterministicInference;
        }

        /// <inheritdoc />
        public void RequestDecision(AgentInfo info, List<ISensor> sensors)
        {
            SendAnalytics(sensors);
            m_AgentId = info.episodeId;
            m_ModelRunner?.PutObservations(info, sensors);
        }

        [Conditional("MLA_UNITY_ANALYTICS_MODULE")]
        void SendAnalytics(IList<ISensor> sensors)
        {
            if (!m_AnalyticsSent)
            {
                m_AnalyticsSent = true;
                Analytics.InferenceAnalytics.InferenceModelSet(
                    m_ModelRunner.Model,
                    m_BehaviorName,
                    m_ModelRunner.InferenceDevice,
                    sensors,
                    m_ActionSpec,
                    m_Actuators
                );
            }
        }

        /// <inheritdoc />
        public ref readonly ActionBuffers DecideAction()
        {
            if (m_ModelRunner == null)
            {
                m_LastActionBuffer = ActionBuffers.Empty;
            }
            else
            {
                m_ModelRunner?.DecideBatch();
                m_LastActionBuffer = m_ModelRunner.GetAction(m_AgentId);
            }
            return ref m_LastActionBuffer;
        }

        public void Dispose()
        {
        }
    }
}

using System;
using TrRebootTools.Shared;

namespace TrRebootTools.Shared
{
    public class MultiStepTaskProgress : ITaskProgress
    {
        private readonly ITaskProgress _inner;
        private readonly int _numSteps;
        private int _currentStep;

        public MultiStepTaskProgress(ITaskProgress inner, int numSteps)
        {
            _inner = inner;
            _numSteps = numSteps;
        }

        public void Begin(string statusText)
        {
            if (_currentStep == 0)
                _inner.Begin(statusText);
        }

        public void Report(float progress)
        {
            _inner.Report((_currentStep + progress) / _numSteps);
        }

        public void End()
        {
            _currentStep++;
            _inner.Report((float)_currentStep / _numSteps);
            if (_currentStep == _numSteps)
                _inner.End();
        }
    }
}

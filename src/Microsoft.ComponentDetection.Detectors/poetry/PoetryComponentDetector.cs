using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Tomlyn;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Poetry.Contracts;

namespace Microsoft.ComponentDetection.Detectors.Poetry
{
    [Export(typeof(IComponentDetector))]
    public class PoetryComponentDetector : FileComponentDetector, IExperimentalDetector
    {
        public override string Id => "Poetry";

        public override IList<string> SearchPatterns { get; } = new List<string> { "poetry.lock" };

        public override IEnumerable<ComponentType> SupportedComponentTypes => new[] { ComponentType.Pip };

        public override int Version { get; } = 2;

        public override IEnumerable<string> Categories => new List<string> { "Python" };

        protected override Task OnFileFound(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
        {
            var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
            var poetryLockFile = processRequest.ComponentStream;
            this.Logger.LogVerbose("Found Poetry lockfile: " + poetryLockFile);

            var reader = new StreamReader(poetryLockFile.Stream);
            var options = new TomlModelOptions
            {
                IgnoreMissingProperties = true,
            };
            var poetryLock = Toml.ToModel<PoetryLock>(reader.ReadToEnd(), options: options);
            poetryLock.Package.ToList().ForEach(package =>
            {
                var isDevelopmentDependency = package.Category != "main";

                if (package.Source != null && package.Source.type == "git")
                {
                    var component = new DetectedComponent(new GitComponent(new Uri(package.Source.url), package.Source.resolved_reference));
                    singleFileComponentRecorder.RegisterUsage(component, isDevelopmentDependency: isDevelopmentDependency);
                }
                else
                {
                    var component = new DetectedComponent(new PipComponent(package.Name, package.Version));
                    singleFileComponentRecorder.RegisterUsage(component, isDevelopmentDependency: isDevelopmentDependency);
                }
            });
            return Task.CompletedTask;
        }
    }
}

using OpenTelemetry.Trace;

namespace Playground.Microservice.Api.Host;

public class CustomSampler : Sampler
{
    public override SamplingResult ShouldSample(in SamplingParameters parameters)
    {
        if (parameters.Tags?.ToDictionary().ContainsKey("ignite_health_check") == true)
            return new SamplingResult(SamplingDecision.Drop);
        //// Only record spans whose name contains "MyOperation"
        //if (parameters.Name.Contains(Diagnostics.DeliverOutboxActivityName))
        //{
        //    return new SamplingResult(SamplingDecision.Drop);

        //}
        return new SamplingResult(SamplingDecision.RecordAndSample);
    }
}
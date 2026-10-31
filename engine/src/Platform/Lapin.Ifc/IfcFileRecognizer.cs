using Lapin.Model;
using Lapin.Platform;
using Lapin.Ifc;

namespace Lapin.Ifc;

/// <summary>
/// Offered by a module that rebuilds what a foreign IFC file holds outside the
/// spatial structure — an IfcAlignment is aggregated by the project, not
/// contained in a storey, so the element recognisers never see it. Asked once
/// per import, before the elements, with the whole file: it makes what it can
/// natively (in <paramref name="storeyId"/> when it needs a storey) and returns
/// the file ids it has taken (the alignment and what is nested in it), which
/// the importer then leaves alone instead of bringing them as references.
/// </summary>
public interface IIfcFileRecognizer
{
    IEnumerable<int> Recognize(IEdit edit, StepFile file, string storeyId, RecognitionContext ctx);
}

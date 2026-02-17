using Sh.Autofit.OrderBoard.Web.Models;

namespace Sh.Autofit.OrderBoard.Web.Services;

public interface IStageEngine
{
    string ComputeStage(IEnumerable<AppOrderLink> links);
}

public class StageEngine : IStageEngine
{
    /// <summary>
    /// Computes the authoritative stage for an AppOrder based on its linked Stock documents.
    /// Priority: PACKING > DOC_IN_PC > ORDER_PRINTED > ORDER_IN_PC
    /// </summary>
    public string ComputeStage(IEnumerable<AppOrderLink> links)
    {
        var linkList = links.ToList();
        if (linkList.Count == 0) return "ORDER_IN_PC";

        // Check docs (DocumentId in 1,4,7) — invoice, delivery note, quote
        var docs = linkList.Where(l => l.DocumentId is 1 or 4 or 7).ToList();
        var orders = linkList.Where(l => l.DocumentId == 11).ToList();

        // "Handled" means Status=1 OR disappeared (IsPresent=false)
        bool docHandled = docs.Any(d => d.Status == 1 || !d.IsPresent);
        bool docInPc = docs.Any(d => d.Status == 0 && d.IsPresent);
        bool orderHandled = orders.Any(o => o.Status == 1 || !o.IsPresent);
        bool orderInPc = orders.Any(o => o.Status == 0 && o.IsPresent);

        // Priority order (most advanced stage wins)
        if (docHandled) return "PACKING";
        if (docInPc) return "DOC_IN_PC";
        if (orderHandled) return "ORDER_PRINTED";
        if (orderInPc) return "ORDER_IN_PC";

        // Fallback: if all links are somehow neither, default
        return "ORDER_IN_PC";
    }
}

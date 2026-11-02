using System.Runtime.CompilerServices;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A value derived from the model, kept per entity (immutable) and model
/// revision, with what was read to make it. A geometry producer sees the model
/// through a <see cref="TrackingView"/> and is rebuilt when one of its reads
/// changes; the first producer that misses the cache computes through a
/// tracking view of its own, and every later one gets the same reads added to
/// its view (a storey's room bounds, a roof's shape), so each depends on what the value depends
/// on. The value is computed through a tracking view, so what reads it under a
/// narrow key (a cutter's <see cref="TrackingView.CutKey"/>) still does.
/// </summary>
public sealed class RevisionCache<T>
{
    sealed record Hit(long Revision, T Value, HashSet<string> Reads);
    readonly ConditionalWeakTable<Entity, Hit> _cache = new();

    public T Get(Entity key, IModelView m, Func<IModelView, T> compute)
    {
        var outer = m as TrackingView;
        if (_cache.TryGetValue(key, out var hit) && hit.Revision == m.Revision)
        {
            outer?.Read.UnionWith(hit.Reads);
            return hit.Value;
        }
        // Recorded on the model itself, not through the caller's view: its reads land in the caller's view only as recorded.
        var rec = new TrackingView(outer?.Inner ?? m);
        rec.Get(key.Id);
        var value = compute(rec);
        _cache.AddOrUpdate(key, new Hit(m.Revision, value, rec.Read));
        outer?.Read.UnionWith(rec.Read);
        return value;
    }
}

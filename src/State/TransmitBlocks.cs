using System.Collections;

namespace HidePlayers.State;

public sealed class TransmitBlocks(int capacity, Action<int, int, bool> sink)
{
    private readonly Dictionary<uint, (int EntityIndex, BitArray Recipients)> blocks = [];
    private readonly Dictionary<int, uint> handlesByIndex = [];

    public void Set(uint handle, int entityIndex, int viewerSlot, bool blocked)
    {
        if (handlesByIndex.TryGetValue(entityIndex, out var previousHandle) && previousHandle != handle)
            Remove(previousHandle);
        if (!blocks.TryGetValue(handle, out var entry))
        {
            if (!blocked)
                return;
            entry = (entityIndex, new BitArray(capacity));
            blocks.Add(handle, entry);
            handlesByIndex.Add(entityIndex, handle);
        }
        UpdatePair(entry, viewerSlot, blocked);
    }

    public void Remove(uint handle)
    {
        if (!blocks.Remove(handle, out var entry))
            return;
        for (var slot = 0; slot < capacity; slot++)
            UpdatePair(entry, slot, false);
        handlesByIndex.Remove(entry.EntityIndex);
    }

    public void RemoveViewer(int slot)
    {
        foreach (var entry in blocks.Values)
            UpdatePair(entry, slot, false);
    }

    public void Clear()
    {
        foreach (var entry in blocks.Values)
            for (var slot = 0; slot < capacity; slot++)
                UpdatePair(entry, slot, false);
        blocks.Clear();
        handlesByIndex.Clear();
    }

    private void UpdatePair((int EntityIndex, BitArray Recipients) entry, int slot, bool blocked)
    {
        if (entry.Recipients[slot] == blocked)
            return;
        entry.Recipients[slot] = blocked;
        sink(slot, entry.EntityIndex, blocked);
    }
}

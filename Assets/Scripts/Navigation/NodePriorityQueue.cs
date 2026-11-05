using System;

// Montículo mínimo indexado. Cada nodo tiene una sola entrada pendiente.
internal sealed class NodePriorityQueue
{
    readonly (int Node, float Priority, long Order)[] _heap;
    readonly int[] _positions;
    int _count;
    long _nextOrder;

    public NodePriorityQueue(int nodeCount)
    {
        _heap = new (int Node, float Priority, long Order)[nodeCount];
        _positions = new int[nodeCount];
        Array.Fill(_positions, -1);
    }

    public void EnqueueOrDecrease(int node, float priority)
    {
        int index = _positions[node];
        if (index < 0)
        {
            index = _count++;
            _heap[index] = (node, priority, _nextOrder++);
            _positions[node] = index;
        }
        else
        {
            if (priority >= _heap[index].Priority) return;
            _heap[index].Priority = priority;
        }

        while (index > 0)
        {
            int parent = (index - 1) / 2;
            if (!ComesBefore(index, parent)) break;
            Swap(index, parent);
            index = parent;
        }
    }

    public bool TryDequeue(out int node)
    {
        if (_count == 0)
        {
            node = -1;
            return false;
        }

        node = _heap[0].Node;
        _positions[node] = -1;
        _count--;
        if (_count == 0) return true;

        _heap[0] = _heap[_count];
        _positions[_heap[0].Node] = 0;

        int index = 0;
        while (index < _count / 2)
        {
            int child = index * 2 + 1;
            int right = child + 1;
            if (right < _count && ComesBefore(right, child)) child = right;
            if (!ComesBefore(child, index)) break;

            Swap(index, child);
            index = child;
        }

        return true;
    }

    bool ComesBefore(int first, int second)
    {
        var a = _heap[first];
        var b = _heap[second];
        return a.Priority < b.Priority || (a.Priority == b.Priority && a.Order < b.Order);
    }

    void Swap(int first, int second)
    {
        (_heap[first], _heap[second]) = (_heap[second], _heap[first]);
        _positions[_heap[first].Node] = first;
        _positions[_heap[second].Node] = second;
    }
}

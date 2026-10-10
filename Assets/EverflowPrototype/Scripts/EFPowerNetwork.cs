using System.Collections.Generic;
using UnityEngine;

// Explicit directed links, not proximity or material-based electricity.
// The visited set also makes cycles safe.
[DefaultExecutionOrder(-100)]
public class EFPowerNetwork : MonoBehaviour
{
    public EFNode[] nodes = new EFNode[0];
    readonly Queue<EFNode> pending = new Queue<EFNode>();
    readonly HashSet<EFNode> visited = new HashSet<EFNode>();

    void Update()
    {
        pending.Clear();
        visited.Clear();
        foreach (EFNode node in nodes)
        {
            if (node == null) continue;
            node.Powered = false;
            if (node.isActiveAndEnabled && node.isPowerSource && node.SourceOn)
                pending.Enqueue(node);
        }
        while (pending.Count > 0)
        {
            EFNode node = pending.Dequeue();
            if (node == null || !node.isActiveAndEnabled || !visited.Add(node) || !node.Conducts)
                continue;
            node.Powered = true;
            foreach (EFNode output in node.outputs)
                if (output != null) pending.Enqueue(output);
        }
        foreach (EFNode node in nodes)
            if (node != null && node.isActiveAndEnabled) node.RefreshAppearance();
    }
}

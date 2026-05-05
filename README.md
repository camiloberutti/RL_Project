\# Identified things to fix:



The normalization to feed the NN is wrong. It was hardcoded and I dragged the error. I need to recalculate the max possible distance from the cannon to the target. To fix add:

1 - Calculation:

private float maxPossibleDistance;

public override void Initialize()
{
// Compute the true maximum distance from spawn ranges
// Worst case: target is at the farthest corner of the spawn area
float maxX = Mathf.Max(Mathf.Abs(spawnRangeX.x), Mathf.Abs(spawnRangeX.y));
float maxZ = Mathf.Max(Mathf.Abs(spawnRangeZ.x), Mathf.Abs(spawnRangeZ.y));
float maxY = targetZone.localPosition.y; // height difference is fixed
maxPossibleDistance = Mathf.Sqrt(maxX \* maxX + maxY \* maxY + maxZ \* maxZ);
// ... rest of existing Initialize code
}

2 - In collect observations:

sensor.AddObservation(toTarget.magnitude / maxPossibleDistance);



\# Identified things to improve:



* Observe flight of bird and compare distance from it to the target, maybe add a penalty for large misses.


using System.Collections.Generic;
using UnityEngine;

namespace CalgaryCycling
{
    // Spatial index of every centreline segment. Props are tested against the
    // whole route, including return bends, after applying their actual transforms.
    public sealed class RouteClearance
    {
        const float CellSize = 16f;
        readonly IReadOnlyList<Vector3> points;
        readonly Dictionary<Vector2Int,List<int>> cells = new Dictionary<Vector2Int,List<int>>();
        readonly HashSet<int> visited = new HashSet<int>();
        readonly float halfWidth;

        public RouteClearance(RoutePath route,float corridorHalfWidth)
        {
            points=route.Samples;
            halfWidth=corridorHalfWidth;
            for(int i=1;i<points.Count;i++)
            {
                Vector3 min=Vector3.Min(points[i-1],points[i]),max=Vector3.Max(points[i-1],points[i]);
                for(int x=Cell(min.x);x<=Cell(max.x);x++)
                    for(int z=Cell(min.z);z<=Cell(max.z);z++)
                    {
                        var key=new Vector2Int(x,z);
                        if(!cells.TryGetValue(key,out var list)){list=new List<int>();cells.Add(key,list);}
                        list.Add(i);
                    }
            }
        }

        public bool Overlaps(Bounds footprint)
        {
            Vector3 min=footprint.min-Vector3.one*halfWidth,max=footprint.max+Vector3.one*halfWidth;
            visited.Clear();
            for(int x=Cell(min.x);x<=Cell(max.x);x++)
                for(int z=Cell(min.z);z<=Cell(max.z);z++)
                {
                    if(!cells.TryGetValue(new Vector2Int(x,z),out var list))continue;
                    foreach(int i in list)
                    {
                        if(!visited.Add(i))continue;
                        Vector3 a=points[i-1],delta=points[i]-a;
                        float enter=0,exit=1;
                        if(Clip(a.x,delta.x,min.x,max.x,ref enter,ref exit) &&
                            Clip(a.z,delta.z,min.z,max.z,ref enter,ref exit))return true;
                    }
                }
            return false;
        }

        static bool Clip(float origin,float delta,float min,float max,ref float enter,ref float exit)
        {
            if(Mathf.Abs(delta)<.00001f)return origin>=min && origin<=max;
            float a=(min-origin)/delta,b=(max-origin)/delta;
            enter=Mathf.Max(enter,Mathf.Min(a,b));exit=Mathf.Min(exit,Mathf.Max(a,b));
            return enter<=exit;
        }
        static int Cell(float value) { return Mathf.FloorToInt(value/CellSize); }

        public static bool TryFootprint(Transform instance,Transform reference,out Bounds bounds)
        {
            bounds=default;
            bool found=false;
            foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if(!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))continue;
                Bounds local=renderer.localBounds;
                Matrix4x4 matrix=reference.worldToLocalMatrix*renderer.transform.localToWorldMatrix;
                for(int i=0;i<8;i++)
                {
                    var corner=local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    var p=matrix.MultiplyPoint3x4(corner);
                    if(!found){bounds=new Bounds(p,Vector3.zero);found=true;}else bounds.Encapsulate(p);
                }
            }
            return found;
        }
    }
}

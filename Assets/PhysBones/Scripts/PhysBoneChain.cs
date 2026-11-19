using System.Collections.Generic;
using UnityEngine;

namespace PhysBones
{
    // A simulated bone chain. Implements a PhysBone-style solver:
    // each bone with exactly one included child becomes a "joint" that aims at the
    // simulated position of that child (VRChat multiChildType = Ignore behaviour, so
    // a bone with several children is treated as a fixed split point and each branch
    // is solved independently). Verlet integration + position constraints give the
    // pull / spring / stiffness / gravity / immobile / limit / collider behaviours.
    public class PhysBoneChain
    {
        class Joint
        {
            public Transform t;            // bone that gets rotated
            public Vector3 childLocalPos;  // child.localPosition at bind (defines the aim axis + rest length)
            public Vector3 tip;            // simulated world position of the child
            public Vector3 prevTip;
        }

        public string name;
        public int substeps = 2;
        public Vector3 gravityDir = Vector3.down;

        readonly ChainConfig cfg;
        Transform root;
        List<PhysBoneCollider> colliders;

        readonly List<Joint> joints = new List<Joint>();
        readonly List<Transform> allBones = new List<Transform>();      // every included bone, for per-frame rest reset
        readonly List<Quaternion> bindLocalRot = new List<Quaternion>();

        Vector3 prevRootPos;
        bool bound;

        public bool IsBound { get { return bound; } }
        public int JointCount { get { return joints.Count; } }

        // Every bone this chain rotates (for telling which colliders can be measured once per frame).
        public void AddMovedBones(HashSet<Transform> into)
        {
            for (int i = 0; i < allBones.Count; i++) into.Add(allBones[i]);
        }

        public PhysBoneChain(ChainConfig c)
        {
            cfg = c;
            name = c.name;
        }

        public bool Bind(Transform rootBone, HashSet<string> ignoreLower, List<PhysBoneCollider> cols)
        {
            joints.Clear();
            allBones.Clear();
            bindLocalRot.Clear();

            root = rootBone;
            colliders = cols;
            if (root == null) return false;

            BuildJoints(root, ignoreLower);

            prevRootPos = root.position;
            bound = joints.Count > 0;
            return bound;
        }

        void BuildJoints(Transform t, HashSet<string> ignoreLower)
        {
            List<Transform> kids = new List<Transform>();
            for (int i = 0; i < t.childCount; i++)
            {
                Transform c = t.GetChild(i);
                if (ignoreLower != null && ignoreLower.Contains(c.name.ToLowerInvariant())) continue;
                kids.Add(c);
            }

            allBones.Add(t);
            bindLocalRot.Add(t.localRotation);

            if (kids.Count == 1)
            {
                Joint j = new Joint();
                j.t = t;
                j.childLocalPos = kids[0].localPosition;
                j.tip = kids[0].position;
                j.prevTip = j.tip;
                joints.Add(j);
            }
            // kids.Count == 0 (leaf) or > 1 (split point): t is a fixed anchor, not aimed.

            for (int i = 0; i < kids.Count; i++)
                BuildJoints(kids[i], ignoreLower);
        }

        public void Solve(float dt)
        {
            if (!bound) return;

            Vector3 rootNow = root.position;
            Vector3 rootDelta = rootNow - prevRootPos;
            prevRootPos = rootNow;

            // A jump this large is a teleport (avatar reload, seat change, scene reset), not
            // motion. Carrying it into the Verlet history whips every chain; shift the whole
            // sim state along instead so the chain arrives already settled.
            if (rootDelta.magnitude > 1f)
            {
                for (int i = 0; i < joints.Count; i++)
                {
                    joints[i].tip += rootDelta;
                    joints[i].prevTip += rootDelta;
                }
                rootDelta = Vector3.zero;
            }

            float immobile = Mathf.Clamp01(cfg.immobile);
            if (immobile > 0f)
            {
                // Shift history along with the avatar so root motion is partly ignored.
                for (int i = 0; i < joints.Count; i++)
                    joints[i].prevTip += rootDelta * immobile;
            }

            int sub = Mathf.Clamp(substeps, 1, 8);
            float sdt = dt / sub;
            for (int s = 0; s < sub; s++)
                Substep(sdt);
        }

        void Substep(float dt)
        {
            float damping = Mathf.Lerp(0.20f, 0.02f, Mathf.Clamp01(cfg.spring));
            float pullStrength = Mathf.Clamp01(cfg.pull) * 0.4f;
            float stiffBlend = Mathf.Clamp01(cfg.stiffness) * 0.3f;
            float gPow = cfg.gravity * 9.8f;
            float falloff = Mathf.Clamp01(cfg.gravityFalloff);
            float maxStretch = Mathf.Max(0f, cfg.maxStretch);
            bool limit = cfg.limitType == "angle";
            float boneRadius = Mathf.Max(0f, cfg.radius);

            // Reset every included bone to its rest pose so each joint's rest direction
            // is measured from its (already-aimed) parent this substep.
            for (int i = 0; i < allBones.Count; i++)
            {
                Quaternion want = bindLocalRot[i];
                Quaternion have = allBones[i].localRotation;
                if (have.x != want.x || have.y != want.y || have.z != want.z || have.w != want.w)
                    allBones[i].localRotation = want;
            }

            for (int i = 0; i < joints.Count; i++)
            {
                Joint j = joints[i];
                Vector3 parentPos;
                Quaternion jointRot;
                j.t.GetPositionAndRotation(out parentPos, out jointRot);
                Vector3 restTip = j.t.TransformPoint(j.childLocalPos);

                // The vector maths below is written out per component instead of ~30 Vector3
                // operator calls. Each (float) cast sits where a Vector3 operator or a float
                // argument would round, so every value is identical to the operator version
                // (Mono keeps unrounded intermediates otherwise).
                float rx = (float)(restTip.x - parentPos.x), ry = (float)(restTip.y - parentPos.y), rz = (float)(restTip.z - parentPos.z);
                float boneLen = (float)System.Math.Sqrt(rx * rx + ry * ry + rz * rz);
                if (boneLen < 1e-6f) continue;

                // Verlet inertia, capped: a tip can carry at most two bone-lengths of motion
                // per substep. Anything above that is an artifact (teleport remnant, constraint
                // fight), and uncapped it feeds back through the length constraint as whip.
                Vector3 tip = j.tip, prevTip = j.prevTip;
                float keep = (float)(1f - damping);
                float vx = (float)((float)(tip.x - prevTip.x) * keep);
                float vy = (float)((float)(tip.y - prevTip.y) * keep);
                float vz = (float)((float)(tip.z - prevTip.z) * keep);
                float maxVel = boneLen * 2f;
                if ((float)(vx * vx + vy * vy + vz * vz) > maxVel * maxVel)
                {
                    float vm = (float)System.Math.Sqrt(vx * vx + vy * vy + vz * vz);
                    if (vm > 1E-05f) { vx = (float)(vx / vm); vy = (float)(vy / vm); vz = (float)(vz / vm); }
                    else { vx = 0f; vy = 0f; vz = 0f; }
                    vx = (float)(vx * maxVel); vy = (float)(vy * maxVel); vz = (float)(vz * maxVel);
                }

                // Gravity with rest-pose falloff.
                float gFactor = 1f;
                if (falloff > 0f)
                {
                    float ex = (float)(tip.x - restTip.x), ey = (float)(tip.y - restTip.y), ez = (float)(tip.z - restTip.z);
                    float disp = Mathf.Clamp01((float)System.Math.Sqrt(ex * ex + ey * ey + ez * ez) / boneLen);
                    gFactor = Mathf.Lerp(1f, disp, falloff);
                }
                float gk = (float)(gPow * gFactor);
                float gx = (float)((float)((float)(gravityDir.x * gk) * dt) * dt);
                float gy = (float)((float)((float)(gravityDir.y * gk) * dt) * dt);
                float gz = (float)((float)((float)(gravityDir.z * gk) * dt) * dt);

                float nx = (float)((float)(tip.x + vx) + gx);
                float ny = (float)((float)(tip.y + vy) + gy);
                float nz = (float)((float)(tip.z + vz) + gz);

                // Pull back toward the animated rest position (Vector3.Lerp, written out).
                float pt = Mathf.Clamp01(pullStrength);
                nx = (float)(nx + (restTip.x - nx) * pt);
                ny = (float)(ny + (restTip.y - ny) * pt);
                nz = (float)(nz + (restTip.z - nz) * pt);

                // Length constraint (allow stretch up to maxStretch, no squish).
                float dx = (float)(nx - parentPos.x), dy = (float)(ny - parentPos.y), dz = (float)(nz - parentPos.z);
                float dlen = (float)System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
                float L = Mathf.Clamp(dlen, boneLen, boneLen * (1f + maxStretch));
                Vector3 next;
                if (dlen > 1e-6f)
                {
                    float q = (float)(L / dlen);
                    next = new Vector3((float)(parentPos.x + (float)(dx * q)), (float)(parentPos.y + (float)(dy * q)), (float)(parentPos.z + (float)(dz * q)));
                }
                else next = restTip;

                // Stiffness: bend the bone back toward its rest direction.
                if (stiffBlend > 0f)
                {
                    // (next - parentPos).normalized and (restTip - parentPos).normalized, written out;
                    // the rest direction's length is boneLen, computed the same way above.
                    float ax = (float)(next.x - parentPos.x), ay = (float)(next.y - parentPos.y), az = (float)(next.z - parentPos.z);
                    float am = (float)System.Math.Sqrt(ax * ax + ay * ay + az * az);
                    Vector3 nd = am > 1E-05f ? new Vector3((float)(ax / am), (float)(ay / am), (float)(az / am)) : Vector3.zero;
                    Vector3 rd = boneLen > 1E-05f ? new Vector3((float)(rx / boneLen), (float)(ry / boneLen), (float)(rz / boneLen)) : Vector3.zero;
                    Vector3 sl = Vector3.Slerp(nd, rd, stiffBlend);
                    float bm = (float)System.Math.Sqrt(sl.x * sl.x + sl.y * sl.y + sl.z * sl.z);
                    float bx = 0f, by = 0f, bz = 0f;
                    if (bm > 1E-05f) { bx = (float)(sl.x / bm); by = (float)(sl.y / bm); bz = (float)(sl.z / bm); }
                    next = new Vector3((float)(parentPos.x + (float)(bx * L)), (float)(parentPos.y + (float)(by * L)), (float)(parentPos.z + (float)(bz * L)));
                }

                // Angle limit cone.
                if (limit)
                {
                    Vector3 rd = restTip - parentPos;
                    Vector3 nd = next - parentPos;
                    float ang = Vector3.Angle(rd, nd);
                    if (ang > cfg.maxAngle && ang > 1e-4f)
                    {
                        Vector3 cd = Vector3.Slerp(rd.normalized, nd.normalized, cfg.maxAngle / ang).normalized;
                        next = parentPos + cd * L;
                    }
                }

                // Colliders + length, iterated. One pass is not enough: restoring the bone
                // length after a push-out can re-embed the tip in the collider it just left,
                // so the pair is relaxed twice and a final collider pass guarantees the tip is
                // handed back outside every volume (the tiny length error this leaves does not
                // matter, because only the tip's direction drives the bone rotation below).
                Vector3 beforeCollision = next;
                if (colliders != null && colliders.Count > 0)
                {
                    for (int iter = 0; iter < 2; iter++)
                    {
                        for (int ci = 0; ci < colliders.Count; ci++)
                            colliders[ci].Resolve(ref next, boneRadius);

                        Vector3 d2 = next - parentPos;
                        float l2 = d2.magnitude;
                        if (l2 > 1e-6f) next = parentPos + d2 * (L / l2);
                    }
                    for (int ci = 0; ci < colliders.Count; ci++)
                        colliders[ci].Resolve(ref next, boneRadius);
                }

                j.prevTip = j.tip;
                j.tip = next;

                // The push-out must not read as motion on the next substep. Without this, the
                // whole collider correction lands in (tip - prevTip) and Verlet re-injects it
                // as velocity every frame, so a rest pose inside a collider oscillates forever
                // instead of settling on the surface (the "exploding hair" failure).
                j.prevTip += next - beforeCollision;

                // Aim the bone so its rest child-direction points at the solved tip.
                float wx = (float)(next.x - parentPos.x), wy = (float)(next.y - parentPos.y), wz = (float)(next.z - parentPos.z);
                if ((float)(rx * rx + ry * ry + rz * rz) > 1e-12f && (float)(wx * wx + wy * wy + wz * wz) > 1e-12f)
                    j.t.rotation = Quaternion.FromToRotation(new Vector3(rx, ry, rz), new Vector3(wx, wy, wz)) * jointRot;
            }
        }
    }
}

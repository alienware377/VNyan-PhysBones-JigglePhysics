using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace JigglePhysics
{
    // No-click check of the copy / paste + groups section. Runs once at startup only when the flag
    // file jigglephysics.groupstest.on exists next to the config (it is deleted first). It never
    // saves, and it ends by reloading the config from disk, so the saved settings stay untouched.
    public partial class JigglePhysicsPlugin
    {
        const string GROUPS_TEST_FLAG = "jigglephysics.groupstest.on";
        int gtPass, gtFail;

        void Start()
        {
            string flag = Path.Combine(Application.persistentDataPath, GROUPS_TEST_FLAG);
            if (!File.Exists(flag)) return;
            try { File.Delete(flag); } catch { }
            StartCoroutine(GroupsSelfTest());
        }

        void GT(bool ok, string what)
        {
            if (ok) gtPass++; else gtFail++;
            Debug.Log("[Jiggle] GROUPSTEST " + (ok ? "ok   " : "FAIL ") + what);
        }

        static bool SameTuning(Tuning x, Tuning y)
        {
            return x.weight == y.weight && x.bounce == y.bounce && x.stiffness == y.stiffness
                && x.damping == y.damping && x.pull == y.pull && x.maxAngle == y.maxAngle
                && x.stretch == y.stretch && x.squish == y.squish && x.radius == y.radius
                && x.limitType == y.limitType && x.selfCollide == y.selfCollide;
        }

        static Rect WorldRect(RectTransform rt)
        {
            Vector3[] c = new Vector3[4];
            rt.GetWorldCorners(c);
            float x0 = Mathf.Min(c[0].x, c[2].x), x1 = Mathf.Max(c[0].x, c[2].x);
            float y0 = Mathf.Min(c[0].y, c[2].y), y1 = Mathf.Max(c[0].y, c[2].y);
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        string ScreenRect(GameObject go)
        {
            if (go == null) return "none";
            RectTransform rt = go.GetComponent<RectTransform>();
            Canvas cv = go.GetComponentInParent<Canvas>();
            Camera cam = (cv == null || cv.renderMode == RenderMode.ScreenSpaceOverlay) ? null : cv.worldCamera;
            Vector3[] c = new Vector3[4];
            rt.GetWorldCorners(c);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, c[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
            return Mathf.RoundToInt(a.x) + "," + Mathf.RoundToInt(Screen.height - b.y) + " to "
                + Mathf.RoundToInt(b.x) + "," + Mathf.RoundToInt(Screen.height - a.y)
                + " (screen " + Screen.width + "x" + Screen.height + ")";
        }

        void CheckLayout()
        {
            Rect wr = WorldRect(window.GetComponent<RectTransform>());
            List<KeyValuePair<string, Rect>> mine = new List<KeyValuePair<string, Rect>>();
            List<KeyValuePair<string, Rect>> old = new List<KeyValuePair<string, Rect>>();
            foreach (Transform t in window.transform)
            {
                RectTransform rt = t as RectTransform;
                if (rt == null || !t.gameObject.activeSelf || t.name == "Grp_Picker") continue;
                Rect r = WorldRect(rt);
                r = Rect.MinMaxRect(r.xMin + 0.5f, r.yMin + 0.5f, r.xMax - 0.5f, r.yMax - 0.5f);
                if (t.name.StartsWith("Grp_")) mine.Add(new KeyValuePair<string, Rect>(t.name, r));
                else old.Add(new KeyValuePair<string, Rect>(t.name, r));
            }
            int outside = 0, clash = 0;
            foreach (KeyValuePair<string, Rect> m in mine)
            {
                Rect r = m.Value;
                if (r.xMin < wr.xMin || r.xMax > wr.xMax || r.yMin < wr.yMin || r.yMax > wr.yMax)
                { outside++; Debug.Log("[Jiggle] GROUPSTEST outside the window: " + m.Key); }
                foreach (KeyValuePair<string, Rect> o in old)
                    if (r.Overlaps(o.Value)) { clash++; Debug.Log("[Jiggle] GROUPSTEST overlap: " + m.Key + " / " + o.Key); }
                foreach (KeyValuePair<string, Rect> o in mine)
                    if (string.CompareOrdinal(m.Key, o.Key) < 0 && r.Overlaps(o.Value))
                    { clash++; Debug.Log("[Jiggle] GROUPSTEST overlap: " + m.Key + " / " + o.Key); }
            }
            GT(mine.Count >= 15, "built " + mine.Count + " new controls");
            GT(outside == 0, "new controls inside the window");
            GT(clash == 0, "no overlaps with each other or the old controls");
        }

        IEnumerator GroupsSelfTest()
        {
            float until = Time.realtimeSinceStartup + 60f;
            while ((window == null || boundAvatar == null) && Time.realtimeSinceStartup < until)
                yield return new WaitForSeconds(0.5f);
            if (window == null || config == null || config.bones == null || config.bones.Count < 2)
            {
                Debug.Log("[Jiggle] GROUPSTEST skipped: needs the window and 2+ bones");
                yield break;
            }
            yield return new WaitForSeconds(2f);

            window.SetActive(true);
            window.transform.SetAsLastSibling();
            RefreshBoneList();
            yield return null;
            Canvas.ForceUpdateCanvases();
            CheckLayout();

            JiggleBoneConfig a = config.bones[0], b = config.bones[1];
            Tuning a0 = Tuning.Of(a), b0 = Tuning.Of(b);
            int groups0 = Groups().Count;

            SelectBone(0);
            OnCopyClicked();
            GT(clipboard != null && clipboard.from == a.name, "copy");
            SelectBone(1);
            float marker = a.weight > 0.5f ? 0.1f : 0.9f;
            b.weight = marker;
            OnPasteClicked();
            GT(SameTuning(Tuning.Of(b), a0), "paste copies every shown setting");
            OnUndoClicked();
            GT(b.weight == marker, "undo puts it back");

            SelectBone(0);
            OnGroupNew();
            JiggleGroupConfig g = ActiveGroup();
            GT(Groups().Count == groups0 + 1 && g != null && InGroup(g, a), "new group holds the shown bone");
            GT(picker != null && picker.activeSelf && pickerRows.Count == config.bones.Count, "picker lists every bone");
            TickShown(true);
            GT(MembersOf(g).Count == config.bones.Count, "tick shown");
            TickShown(false);
            GT(MembersOf(g).Count == 0, "untick shown");
            pickerFilter.text = b.name;
            bool hasB = false;
            foreach (KeyValuePair<JiggleBoneConfig, Button> kv in pickerRows) if (kv.Key == b) hasB = true;
            GT(hasB && pickerRows.Count <= config.bones.Count, "find box filters (" + pickerRows.Count + " shown)");
            pickerFilter.text = "";
            GT(pickerRows.Count == config.bones.Count, "clearing the find box shows all again");
            OnPickerRowClicked(b);
            GT(InGroup(g, b) && !InGroup(g, a), "a row click ticks one bone");
            SelectBone(0);
            OnToggleThisClicked();
            GT(InGroup(g, a), "+/- this bone adds the shown bone");

            g.linked = true;
            float w = a.weight > 0.5f ? 0.2f : 0.8f;
            OnSliderChanged("weight", w);
            GT(b.weight == w, "linked edit reaches the other member");
            g.linked = false;
            OnSliderChanged("weight", w * 0.5f);
            GT(b.weight == w, "unlinked edit stays on one bone");
            OnApplyGroupClicked();
            GT(SameTuning(Tuning.Of(b), Tuning.Of(a)), "apply to group");
            clipboard = b0;
            OnPasteGroupClicked();
            GT(SameTuning(Tuning.Of(a), b0) && SameTuning(Tuning.Of(b), b0), "paste to group");

            ForgetInGroups(b.name);
            GT(!InGroup(g, b), "a removed bone leaves its groups");
            GT(groupNameField != null && groupNameField.text == g.name, "name box shows the group");

            // Pose for a screenshot: two members, picker open.
            SetMember(g, a, true);
            SetMember(g, b, true);
            g.name = "test group";
            g.linked = true;
            RefreshGroupUI();
            RebuildPickerRows();
            Canvas.ForceUpdateCanvases();
            Debug.Log("[Jiggle] GROUPSTEST HOLD 25s window " + ScreenRect(window) + " | picker " + ScreenRect(picker));
            yield return new WaitForSeconds(25f);

            OnGroupDelete();
            GT(Groups().Count == groups0, "delete group");

            OnReloadClicked();
            GT(config.bones.Count >= 2 && SameTuning(Tuning.Of(config.bones[0]), a0)
                && SameTuning(Tuning.Of(config.bones[1]), b0), "reload restored the saved settings");
            ClosePicker();
            window.SetActive(false);
            Debug.Log("[Jiggle] GROUPSTEST DONE pass=" + gtPass + " fail=" + gtFail);
        }
    }
}

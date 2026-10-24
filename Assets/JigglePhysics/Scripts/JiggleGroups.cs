using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace JigglePhysics
{
    // Copy / paste of a bone's settings, and groups of hand-picked bones that share settings.
    // The controls are built at runtime under the prefab window (cloned from its own Save button
    // and limit toggle so they look the same), so the .vnobj bundle did not need a rebuild.
    public partial class JigglePhysicsPlugin
    {
        // Everything the tuning window shows for a bone: what Copy / Paste / groups move around.
        // Per-bone geometry (bone, axis, length) and collider lists are never copied.
        class Tuning
        {
            public string from;
            public float weight, bounce, stiffness, damping, pull, maxAngle, stretch, squish, radius;
            public string limitType;
            public bool selfCollide;

            public static Tuning Of(JiggleBoneConfig b)
            {
                Tuning t = new Tuning();
                t.from = b.name;
                t.weight = b.weight;
                t.bounce = b.bounce;
                t.stiffness = b.stiffness;
                t.damping = b.damping;
                t.pull = b.pull;
                t.maxAngle = b.maxAngle;
                t.stretch = b.stretch;
                t.squish = b.squish;
                t.radius = b.radius;
                t.limitType = b.limitType;
                t.selfCollide = b.selfCollide;
                return t;
            }

            public void ApplyTo(JiggleBoneConfig b)
            {
                b.weight = weight;
                b.bounce = bounce;
                b.stiffness = stiffness;
                b.damping = damping;
                b.pull = pull;
                b.maxAngle = maxAngle;
                b.stretch = stretch;
                b.squish = squish;
                b.radius = radius;
                b.limitType = limitType;
                b.selfCollide = selfCollide;
            }
        }

        const float GRP_PICKER_W = 300f;
        const float GRP_ROW_H = 22f;

        Tuning clipboard;
        // One step of undo for Paste / Paste to group / Apply to group.
        readonly List<KeyValuePair<JiggleBoneConfig, Tuning>> undoList =
            new List<KeyValuePair<JiggleBoneConfig, Tuning>>();
        string undoWhat;

        int groupIndex;
        bool groupUiReady;
        Text clipLabel;
        Text groupInfo;
        InputField groupNameField;
        Toggle groupLinkToggle;
        Button tmplButton;
        Sprite inputSprite;

        GameObject picker;
        Text pickerTitle;
        InputField pickerFilter;
        RectTransform pickerContent;
        ScrollRect pickerScroll;
        readonly List<KeyValuePair<JiggleBoneConfig, Button>> pickerRows =
            new List<KeyValuePair<JiggleBoneConfig, Button>>();

        static readonly Color ROW_IN = new Color(0.22f, 0.42f, 0.28f, 0.95f);
        static readonly Color ROW_OUT = new Color(0.20f, 0.20f, 0.24f, 0.95f);

        // ----- building the section -----

        void SetupGroupsUI()
        {
            if (window == null) return;
            RectTransform wrt = window.GetComponent<RectTransform>();
            if (wrt == null) return;

            tmplButton = FindControl<Button>("Button_Save");
            Toggle tmplToggle = FindControl<Toggle>("Toggle_Limit");
            if (tmplButton != null)
            {
                Image bi = tmplButton.GetComponent<Image>();
                if (bi != null) inputSprite = bi.sprite;
            }

            float W = wrt.rect.width;
            float P = 12f;
            float cw = W - 2f * P;
            float gap = 6f;
            float y = wrt.rect.height - 4f;
            Transform root = window.transform;

            // Divider + title + clipboard readout.
            GameObject line = new GameObject("Grp_Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            PlaceAt(line, root, P, y, cw, 1f);
            line.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.18f);
            line.GetComponent<Image>().raycastTarget = false;
            y += 6f;
            MakeLabel(root, "Grp_Title", "Copy / paste & groups", P, y, 180f, 18f, 13, TextAnchor.MiddleLeft, FontStyle.Bold);
            clipLabel = MakeLabel(root, "Grp_Clip", "clipboard: empty", P + 184f, y, cw - 184f, 18f, 11,
                TextAnchor.MiddleRight, FontStyle.Italic);
            y += 22f;

            // Copy | Paste | Paste to group | Undo paste
            float bw4 = (cw - 3f * gap) / 4f;
            MakeButton(root, "Grp_Copy", "Copy", P, y, bw4, 26f, OnCopyClicked);
            MakeButton(root, "Grp_Paste", "Paste", P + (bw4 + gap), y, bw4, 26f, OnPasteClicked);
            MakeButton(root, "Grp_PasteGroup", "Paste to group", P + 2f * (bw4 + gap), y, bw4, 26f, OnPasteGroupClicked);
            MakeButton(root, "Grp_Undo", "Undo paste", P + 3f * (bw4 + gap), y, bw4, 26f, OnUndoClicked);
            y += 32f;

            // Group  [<] [ name ] [>] [New] [Delete]
            MakeLabel(root, "Grp_Lbl", "Group", P, y, 46f, 26f, 13, TextAnchor.MiddleLeft, FontStyle.Normal);
            float x = P + 48f;
            MakeButton(root, "Grp_Prev", "<", x, y, 26f, 26f, OnGroupPrev);
            x += 26f + gap;
            float nameW = cw - 48f - 26f - 26f - 56f - 62f - 4f * gap;
            groupNameField = MakeInput(root, "Grp_Name", x, y, nameW, 26f, "no groups yet");
            groupNameField.onEndEdit.AddListener(OnGroupNameEdited);
            x += nameW + gap;
            MakeButton(root, "Grp_Next", ">", x, y, 26f, 26f, OnGroupNext);
            x += 26f + gap;
            MakeButton(root, "Grp_New", "New", x, y, 56f, 26f, OnGroupNew);
            x += 56f + gap;
            MakeButton(root, "Grp_Delete", "Delete", x, y, 62f, 26f, OnGroupDelete);
            y += 32f;

            // Pick bones... | +/- this bone | Apply to group | [x] Edit together
            MakeButton(root, "Grp_Pick", "Pick bones...", P, y, bw4, 26f, OnPickClicked);
            MakeButton(root, "Grp_AddThis", "+/- this bone", P + (bw4 + gap), y, bw4, 26f, OnToggleThisClicked);
            MakeButton(root, "Grp_Apply", "Apply to group", P + 2f * (bw4 + gap), y, bw4, 26f, OnApplyGroupClicked);
            if (tmplToggle != null)
            {
                GameObject tg = (GameObject)Instantiate(tmplToggle.gameObject);
                tg.name = "Grp_Link";
                PlaceAt(tg, root, P + 3f * (bw4 + gap) + 2f, y + 3f, bw4, 20f);
                groupLinkToggle = tg.GetComponent<Toggle>();
                groupLinkToggle.onValueChanged = new Toggle.ToggleEvent();
                groupLinkToggle.group = null;
                Text lt = tg.GetComponentInChildren<Text>(true);
                if (lt != null) { lt.text = "Edit together"; lt.horizontalOverflow = HorizontalWrapMode.Overflow; }
                groupLinkToggle.onValueChanged.AddListener(OnGroupLinkToggled);
            }
            y += 30f;

            groupInfo = MakeLabel(root, "Grp_Info", "", P, y, cw, 30f, 11, TextAnchor.UpperLeft, FontStyle.Italic);
            groupInfo.horizontalOverflow = HorizontalWrapMode.Wrap;
            y += 32f;

            wrt.sizeDelta = new Vector2(wrt.sizeDelta.x, wrt.sizeDelta.y + (y + P - wrt.rect.height));

            BuildPicker(W, wrt.rect.height);
            groupUiReady = true;
            RefreshGroupUI();
        }

        void BuildPicker(float windowW, float height)
        {
            picker = new GameObject("Grp_Picker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            PlaceAt(picker, window.transform, windowW + 8f, 0f, GRP_PICKER_W, height);
            Image pimg = picker.GetComponent<Image>();
            Image wimg = window.GetComponent<Image>();
            if (wimg != null) { pimg.sprite = wimg.sprite; pimg.type = wimg.type; pimg.color = wimg.color; }
            else pimg.color = new Color(0.13f, 0.13f, 0.15f, 0.96f);

            float P = 12f;
            float cw = GRP_PICKER_W - 2f * P;
            float y = 10f;
            pickerTitle = MakeLabel(picker.transform, "Grp_PickTitle", "", P, y, cw, 20f, 13, TextAnchor.MiddleCenter, FontStyle.Bold);
            y += 24f;
            MakeLabel(picker.transform, "Grp_FindLbl", "Find", P, y, 40f, 26f, 13, TextAnchor.MiddleLeft, FontStyle.Normal);
            pickerFilter = MakeInput(picker.transform, "Grp_Find", P + 42f, y, cw - 42f, 26f, "type part of a name");
            pickerFilter.onValueChanged.AddListener(delegate(string s) { RebuildPickerRows(); });
            y += 32f;
            float bw3 = (cw - 2f * 6f) / 3f;
            MakeButton(picker.transform, "Grp_TickAll", "Tick shown", P, y, bw3, 26f, delegate { TickShown(true); });
            MakeButton(picker.transform, "Grp_UntickAll", "Untick shown", P + bw3 + 6f, y, bw3, 26f, delegate { TickShown(false); });
            MakeButton(picker.transform, "Grp_PickDone", "Done", P + 2f * (bw3 + 6f), y, bw3, 26f, ClosePicker);
            y += 32f;

            GameObject sc = new GameObject("Grp_Scroll", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
            PlaceAt(sc, picker.transform, P, y, cw, height - y - P);
            sc.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);

            GameObject vp = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
            RectTransform vrt = vp.GetComponent<RectTransform>();
            vrt.SetParent(sc.transform, false);
            vrt.anchorMin = new Vector2(0f, 0f);
            vrt.anchorMax = new Vector2(1f, 1f);
            vrt.pivot = new Vector2(0f, 1f);
            vrt.offsetMin = new Vector2(2f, 2f);
            vrt.offsetMax = new Vector2(-2f, -2f);
            vp.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);

            GameObject ct = new GameObject("Content", typeof(RectTransform));
            pickerContent = ct.GetComponent<RectTransform>();
            pickerContent.SetParent(vrt, false);
            pickerContent.anchorMin = new Vector2(0f, 1f);
            pickerContent.anchorMax = new Vector2(1f, 1f);
            pickerContent.pivot = new Vector2(0f, 1f);
            pickerContent.offsetMin = new Vector2(0f, 0f);
            pickerContent.offsetMax = new Vector2(0f, 0f);
            pickerContent.sizeDelta = new Vector2(0f, 0f);

            pickerScroll = sc.GetComponent<ScrollRect>();
            pickerScroll.content = pickerContent;
            pickerScroll.viewport = vrt;
            pickerScroll.horizontal = false;
            pickerScroll.vertical = true;
            pickerScroll.movementType = ScrollRect.MovementType.Clamped;
            pickerScroll.scrollSensitivity = 18f;

            picker.SetActive(false);
        }

        // ----- small UI helpers -----

        static RectTransform PlaceAt(GameObject go, Transform parent, float x, float y, float w, float h)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
            return rt;
        }

        Text MakeLabel(Transform parent, string name, string text, float x, float y, float w, float h,
            int size, TextAnchor anchor, FontStyle style)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            PlaceAt(go, parent, x, y, w, h);
            Text t = go.GetComponent<Text>();
            t.text = text;
            t.font = uiFont;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = anchor;
            t.color = Color.white;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        // A button that looks like the window's own (a clone of Save); flat fallback without it.
        Button MakeButton(Transform parent, string name, string label, float x, float y, float w, float h,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject go;
            Text t;
            if (tmplButton != null)
            {
                go = (GameObject)Instantiate(tmplButton.gameObject);
                go.name = name;
                PlaceAt(go, parent, x, y, w, h);
                t = go.GetComponentInChildren<Text>(true);
            }
            else
            {
                go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                PlaceAt(go, parent, x, y, w, h);
                go.GetComponent<Image>().color = new Color(0.85f, 0.85f, 0.88f, 1f);
                go.GetComponent<Button>().targetGraphic = go.GetComponent<Image>();
                GameObject tg = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                RectTransform trt = tg.GetComponent<RectTransform>();
                trt.SetParent(go.transform, false);
                trt.anchorMin = new Vector2(0f, 0f);
                trt.anchorMax = new Vector2(1f, 1f);
                trt.offsetMin = new Vector2(0f, 0f);
                trt.offsetMax = new Vector2(0f, 0f);
                t = tg.GetComponent<Text>();
                t.font = uiFont;
                t.color = Color.black;
                t.alignment = TextAnchor.MiddleCenter;
            }
            if (t != null)
            {
                t.text = label;
                t.fontSize = 12;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            Button b = go.GetComponent<Button>();
            b.onClick = new Button.ButtonClickedEvent();
            if (onClick != null) b.onClick.AddListener(onClick);
            go.SetActive(true);
            return b;
        }

        InputField MakeInput(Transform parent, string name, float x, float y, float w, float h, string hint)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            // Built inactive so the InputField's OnEnable sees its text and makes the caret.
            go.SetActive(false);
            PlaceAt(go, parent, x, y, w, h);
            Image img = go.GetComponent<Image>();
            if (inputSprite != null) { img.sprite = inputSprite; img.type = Image.Type.Sliced; }
            img.color = Color.white;

            Text ph = MakeInputText(go.transform, "Placeholder", hint);
            ph.fontStyle = FontStyle.Italic;
            ph.color = new Color(0.45f, 0.45f, 0.45f, 1f);
            Text tx = MakeInputText(go.transform, "Text", "");

            InputField f = go.AddComponent<InputField>();
            f.targetGraphic = img;
            f.textComponent = tx;
            f.placeholder = ph;
            f.lineType = InputField.LineType.SingleLine;
            go.SetActive(true);
            return f;
        }

        Text MakeInputText(Transform parent, string name, string text)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(6f, 1f);
            rt.offsetMax = new Vector2(-6f, -1f);
            Text t = go.GetComponent<Text>();
            t.text = text;
            t.font = uiFont;
            t.fontSize = 13;
            t.color = Color.black;
            t.alignment = TextAnchor.MiddleLeft;
            t.supportRichText = false;
            return t;
        }

        static bool EnterPressed()
        {
            try
            {
                return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                    || Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter);
            }
            catch { return true; } // no legacy input: treat every end-of-edit as a commit
        }

        // ----- lookups -----

        List<JiggleGroupConfig> Groups()
        {
            if (config.groups == null) config.groups = new List<JiggleGroupConfig>();
            return config.groups;
        }

        JiggleGroupConfig ActiveGroup()
        {
            if (config == null) return null;
            List<JiggleGroupConfig> gs = Groups();
            if (gs.Count == 0) { groupIndex = 0; return null; }
            groupIndex = Mathf.Clamp(groupIndex, 0, gs.Count - 1);
            return gs[groupIndex];
        }

        JiggleBoneConfig BoneByName(string name)
        {
            if (config == null || config.bones == null || string.IsNullOrEmpty(name)) return null;
            for (int i = 0; i < config.bones.Count; i++)
            {
                JiggleBoneConfig b = config.bones[i];
                if (b != null && string.Equals(b.name, name, StringComparison.OrdinalIgnoreCase)) return b;
            }
            return null;
        }

        static int IndexIn(JiggleGroupConfig g, string name)
        {
            if (g == null || g.members == null || name == null) return -1;
            for (int i = 0; i < g.members.Count; i++)
                if (string.Equals(g.members[i], name, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        static bool InGroup(JiggleGroupConfig g, JiggleBoneConfig b)
        {
            return b != null && IndexIn(g, b.name) >= 0;
        }

        void SetMember(JiggleGroupConfig g, JiggleBoneConfig b, bool on)
        {
            if (g == null || b == null) return;
            if (g.members == null) g.members = new List<string>();
            int i = IndexIn(g, b.name);
            if (on && i < 0) g.members.Add(b.name);
            else if (!on && i >= 0) g.members.RemoveAt(i);
        }

        // Members of a group that exist in the config (in config order).
        List<JiggleBoneConfig> MembersOf(JiggleGroupConfig g)
        {
            List<JiggleBoneConfig> res = new List<JiggleBoneConfig>();
            if (g == null || config.bones == null) return res;
            foreach (JiggleBoneConfig b in config.bones)
                if (InGroup(g, b)) res.Add(b);
            return res;
        }

        // Every bone that shares a linked group with 'b' (not b itself).
        List<JiggleBoneConfig> LinkedPeers(JiggleBoneConfig b)
        {
            List<JiggleBoneConfig> res = new List<JiggleBoneConfig>();
            if (b == null || config == null || config.groups == null) return res;
            foreach (JiggleGroupConfig g in config.groups)
            {
                if (g == null || !g.linked || !InGroup(g, b)) continue;
                foreach (JiggleBoneConfig o in MembersOf(g))
                    if (o != b && !res.Contains(o)) res.Add(o);
            }
            return res;
        }

        // Called after a bone is removed from the config.
        void ForgetInGroups(string boneName)
        {
            if (config == null || config.groups == null) return;
            foreach (JiggleGroupConfig g in config.groups)
            {
                int i;
                while ((i = IndexIn(g, boneName)) >= 0) g.members.RemoveAt(i);
            }
            RefreshGroupUI();
        }

        string UniqueGroupName(string baseName)
        {
            string n = baseName;
            for (int k = 2; GroupNameTaken(n, null) && k < 1000; k++) n = baseName + " " + k;
            return n;
        }

        bool GroupNameTaken(string name, JiggleGroupConfig except)
        {
            foreach (JiggleGroupConfig g in Groups())
                if (g != except && g != null && string.Equals(g.name, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // ----- live edits that follow linked groups -----

        static void SetTuningValue(JiggleBoneConfig b, string key, float v)
        {
            switch (key)
            {
                case "weight": b.weight = v; break;
                case "bounce": b.bounce = v; break;
                case "stiffness": b.stiffness = v; break;
                case "damping": b.damping = v; break;
                case "pull": b.pull = v; break;
                case "maxAngle": b.maxAngle = v; break;
                case "stretch": b.stretch = v; break;
                case "squish": b.squish = v; break;
                case "radius": b.radius = v; break;
            }
        }

        // ----- copy / paste -----

        void RecordUndo(string what, List<JiggleBoneConfig> targets)
        {
            undoList.Clear();
            foreach (JiggleBoneConfig b in targets)
                undoList.Add(new KeyValuePair<JiggleBoneConfig, Tuning>(b, Tuning.Of(b)));
            undoWhat = what;
        }

        // Writes 't' onto every target; returns how many changed. Rebinds when self-collision moved.
        int ApplyTuning(Tuning t, List<JiggleBoneConfig> targets)
        {
            bool rebind = false;
            foreach (JiggleBoneConfig b in targets)
            {
                if (b.selfCollide != t.selfCollide) rebind = true;
                t.ApplyTo(b);
            }
            if (rebind) RebindAll();
            PushToUI();
            return targets.Count;
        }

        void OnCopyClicked()
        {
            if (selected == null) { SetStatus("pick a bone first"); return; }
            clipboard = Tuning.Of(selected);
            RefreshGroupUI();
            SetStatus("copied the settings of '" + selected.name + "'");
        }

        void OnPasteClicked()
        {
            if (clipboard == null) { SetStatus("nothing copied yet: pick a bone and press Copy"); return; }
            if (selected == null) { SetStatus("pick a bone to paste onto"); return; }
            List<JiggleBoneConfig> targets = new List<JiggleBoneConfig>();
            targets.Add(selected);
            List<JiggleBoneConfig> peers = LinkedPeers(selected);
            targets.AddRange(peers);
            RecordUndo("paste", targets);
            ApplyTuning(clipboard, targets);
            SetStatus("pasted '" + clipboard.from + "' onto '" + selected.name + "'"
                + (peers.Count > 0 ? " and " + peers.Count + " linked group bone(s)" : "") + ". Save to keep.");
        }

        void OnPasteGroupClicked()
        {
            if (clipboard == null) { SetStatus("nothing copied yet: pick a bone and press Copy"); return; }
            JiggleGroupConfig g = ActiveGroup();
            if (g == null) { SetStatus("no group yet: press New to make one"); return; }
            List<JiggleBoneConfig> targets = MembersOf(g);
            if (targets.Count == 0) { SetStatus("group '" + g.name + "' has no bones yet: press Pick bones..."); return; }
            RecordUndo("paste to group", targets);
            ApplyTuning(clipboard, targets);
            SetStatus("pasted '" + clipboard.from + "' onto all " + targets.Count + " bone(s) of '" + g.name + "'. Save to keep.");
        }

        void OnApplyGroupClicked()
        {
            if (selected == null) { SetStatus("pick a bone first"); return; }
            JiggleGroupConfig g = ActiveGroup();
            if (g == null) { SetStatus("no group yet: press New to make one"); return; }
            List<JiggleBoneConfig> targets = MembersOf(g);
            targets.Remove(selected);
            if (targets.Count == 0) { SetStatus("group '" + g.name + "' has no other bones yet: press Pick bones..."); return; }
            RecordUndo("apply to group", targets);
            ApplyTuning(Tuning.Of(selected), targets);
            SetStatus("the " + targets.Count + " other bone(s) of '" + g.name + "' now use the settings of '"
                + selected.name + "'. Save to keep.");
        }

        void OnUndoClicked()
        {
            if (undoList.Count == 0) { SetStatus("nothing to undo"); return; }
            bool rebind = false;
            foreach (KeyValuePair<JiggleBoneConfig, Tuning> kv in undoList)
            {
                if (kv.Key.selfCollide != kv.Value.selfCollide) rebind = true;
                kv.Value.ApplyTo(kv.Key);
            }
            int n = undoList.Count;
            undoList.Clear();
            if (rebind) RebindAll();
            PushToUI();
            SetStatus("undid the last " + undoWhat + " (" + n + " bone(s) back as they were)");
        }

        // ----- groups -----

        void OnGroupNew()
        {
            if (config == null) return;
            JiggleGroupConfig g = new JiggleGroupConfig();
            g.name = UniqueGroupName("group " + (Groups().Count + 1));
            if (selected != null) g.members.Add(selected.name);
            Groups().Add(g);
            groupIndex = Groups().Count - 1;
            RefreshGroupUI();
            OpenPicker();
            SetStatus("made '" + g.name + "': tick its bones in the list on the right, rename it in the box. Save to keep.");
        }

        void OnGroupDelete()
        {
            JiggleGroupConfig g = ActiveGroup();
            if (g == null) { SetStatus("no group to delete"); return; }
            Groups().Remove(g);
            if (groupIndex >= Groups().Count) groupIndex = Groups().Count - 1;
            if (groupIndex < 0) groupIndex = 0;
            if (Groups().Count == 0) ClosePicker();
            RefreshGroupUI();
            SetStatus("deleted group '" + g.name + "' (the bones keep their settings). Save to keep.");
        }

        void OnGroupPrev() { StepGroup(-1); }
        void OnGroupNext() { StepGroup(1); }

        void StepGroup(int d)
        {
            int n = config == null ? 0 : Groups().Count;
            if (n == 0) { SetStatus("no groups yet: press New to make one"); return; }
            groupIndex = ((groupIndex + d) % n + n) % n;
            RefreshGroupUI();
        }

        void OnGroupNameEdited(string text)
        {
            if (suppressCallbacks) return;
            JiggleGroupConfig g = ActiveGroup();
            if (g == null) { RefreshGroupUI(); return; }
            string nm = text == null ? "" : text.Trim();
            if (!EnterPressed() || nm.Length == 0 || nm == g.name) { RefreshGroupUI(); return; }
            if (GroupNameTaken(nm, g)) { SetStatus("there is already a group called '" + nm + "'"); RefreshGroupUI(); return; }
            g.name = nm;
            RefreshGroupUI();
            SetStatus("group renamed to '" + nm + "'. Save to keep.");
        }

        void OnGroupLinkToggled(bool on)
        {
            if (suppressCallbacks) return;
            JiggleGroupConfig g = ActiveGroup();
            if (g == null) { RefreshGroupUI(); return; }
            g.linked = on;
            RefreshGroupUI();
            SetStatus(on ? "editing any bone of '" + g.name + "' now changes all of them"
                         : "bones of '" + g.name + "' are edited one at a time again");
        }

        void OnToggleThisClicked()
        {
            if (selected == null) { SetStatus("pick a bone first"); return; }
            JiggleGroupConfig g = ActiveGroup();
            if (g == null) { SetStatus("no group yet: press New to make one"); return; }
            bool add = !InGroup(g, selected);
            SetMember(g, selected, add);
            RefreshGroupUI();
            RefreshPickerLabels();
            SetStatus((add ? "added '" : "took '") + selected.name + (add ? "' to '" : "' out of '") + g.name + "'. Save to keep.");
        }

        void RefreshGroupUI()
        {
            if (!groupUiReady || config == null) return;
            if (clipLabel != null)
                clipLabel.text = clipboard == null ? "clipboard: empty" : "clipboard: " + clipboard.from;

            JiggleGroupConfig g = ActiveGroup();
            bool wasSuppressed = suppressCallbacks;
            suppressCallbacks = true;
            if (groupNameField != null)
            {
                groupNameField.text = g == null ? "" : g.name;
                groupNameField.interactable = g != null;
            }
            if (groupLinkToggle != null)
            {
                groupLinkToggle.isOn = g != null && g.linked;
                groupLinkToggle.interactable = g != null;
            }
            suppressCallbacks = wasSuppressed;

            if (groupInfo == null) return;
            if (g == null)
            {
                ClosePicker();
                groupInfo.text = "No groups yet. New makes one; tick its bones, then Apply to group or "
                    + "Paste to group gives them all the same settings.";
                return;
            }
            List<JiggleBoneConfig> mem = MembersOf(g);
            List<string> names = new List<string>();
            int shown = 0, chars = 0;
            foreach (JiggleBoneConfig b in mem)
            {
                if (chars > 70) break;
                names.Add(b.name);
                chars += b.name.Length + 2;
                shown++;
            }
            string list = mem.Count == 0 ? "no bones yet"
                : string.Join(", ", names.ToArray()) + (mem.Count > shown ? " (+" + (mem.Count - shown) + " more)" : "");
            string sel = selected == null ? "" : (InGroup(g, selected) ? " The shown bone is in it." : " The shown bone is not in it.");
            groupInfo.text = "Group " + (groupIndex + 1) + "/" + Groups().Count + ", " + mem.Count + " bone(s): " + list + "."
                + (g.linked ? " Edits to one go to all." : "") + sel;

            if (picker != null && picker.activeSelf) RefreshPickerLabels();
            if (picker != null && picker.activeSelf && pickerTitle != null)
                pickerTitle.text = "Bones in '" + g.name + "'";
        }

        // ----- picker -----

        void OnPickClicked()
        {
            if (picker == null) return;
            if (picker.activeSelf) { ClosePicker(); return; }
            if (ActiveGroup() == null) { SetStatus("no group yet: press New to make one"); return; }
            OpenPicker();
        }

        void OpenPicker()
        {
            if (picker == null) return;
            picker.SetActive(true);
            RebuildPickerRows();
            RefreshGroupUI();
        }

        void ClosePicker()
        {
            if (picker != null) picker.SetActive(false);
        }

        bool PickerShows(JiggleBoneConfig b)
        {
            string f = pickerFilter == null ? "" : (pickerFilter.text ?? "").Trim();
            if (f.Length == 0) return true;
            return (b.name ?? "").IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0
                || (b.bone ?? "").IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void RebuildPickerRows()
        {
            if (pickerContent == null || picker == null || !picker.activeSelf) return;
            for (int i = pickerContent.childCount - 1; i >= 0; i--)
                Destroy(pickerContent.GetChild(i).gameObject);
            pickerRows.Clear();

            float y = 0f;
            if (config != null && config.bones != null)
                foreach (JiggleBoneConfig b in config.bones)
                {
                    if (b == null || !PickerShows(b)) continue;
                    JiggleBoneConfig cap = b;
                    GameObject row = new GameObject("Row", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                    PlaceAt(row, pickerContent, 0f, y, GRP_PICKER_W - 24f - 4f, GRP_ROW_H - 2f);
                    Button btn = row.GetComponent<Button>();
                    btn.targetGraphic = row.GetComponent<Image>();
                    btn.onClick.AddListener(delegate { OnPickerRowClicked(cap); });
                    GameObject tg = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                    RectTransform trt = tg.GetComponent<RectTransform>();
                    trt.SetParent(row.transform, false);
                    trt.anchorMin = new Vector2(0f, 0f);
                    trt.anchorMax = new Vector2(1f, 1f);
                    trt.offsetMin = new Vector2(6f, 0f);
                    trt.offsetMax = new Vector2(-2f, 0f);
                    Text t = tg.GetComponent<Text>();
                    t.font = uiFont;
                    t.fontSize = 13;
                    t.color = new Color(0.9f, 0.92f, 1f, 1f);
                    t.alignment = TextAnchor.MiddleLeft;
                    t.horizontalOverflow = HorizontalWrapMode.Overflow;
                    pickerRows.Add(new KeyValuePair<JiggleBoneConfig, Button>(b, btn));
                    y += GRP_ROW_H;
                }
            pickerContent.sizeDelta = new Vector2(0f, y);
            RefreshPickerLabels();
        }

        void RefreshPickerLabels()
        {
            JiggleGroupConfig g = ActiveGroup();
            foreach (KeyValuePair<JiggleBoneConfig, Button> kv in pickerRows)
            {
                if (kv.Value == null) continue;
                bool on = InGroup(g, kv.Key);
                kv.Value.GetComponent<Image>().color = on ? ROW_IN : ROW_OUT;
                Text t = kv.Value.GetComponentInChildren<Text>(true);
                if (t != null) t.text = (on ? "[x]  " : "[ ]  ") + kv.Key.name;
            }
        }

        void OnPickerRowClicked(JiggleBoneConfig b)
        {
            JiggleGroupConfig g = ActiveGroup();
            if (g == null || b == null) return;
            SetMember(g, b, !InGroup(g, b));
            RefreshPickerLabels();
            RefreshGroupUI();
        }

        void TickShown(bool on)
        {
            JiggleGroupConfig g = ActiveGroup();
            if (g == null) return;
            foreach (KeyValuePair<JiggleBoneConfig, Button> kv in pickerRows) SetMember(g, kv.Key, on);
            RefreshPickerLabels();
            RefreshGroupUI();
            SetStatus((on ? "ticked " : "unticked ") + pickerRows.Count + " bone(s) in '" + g.name + "'. Save to keep.");
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PhysBones
{
    // The chain picker at the top of the window as a folder tree: every group is a folder that
    // opens and shuts, with its chains inside, and chains in no group are listed underneath.
    // Picking a folder edits the whole group (every slider change goes to all its chains);
    // picking a chain edits that chain. The prefab's Dropdown stays as the visible box, but a
    // clear button on top of it opens this tree instead of Unity's flat option list.
    public partial class PhysBonePlugin
    {
        const float TREE_ROW_H = 22f;
        const float TREE_INDENT = 18f;
        const float TREE_MAX_H = 380f;

        // When set, the sliders edit every chain of this group ("selectedChain" shows its first chain).
        ChainGroupConfig editGroup;

        GameObject treePanel;
        GameObject treeBlocker;
        RectTransform treeContent;
        ScrollRect treeScroll;
        float treeX, treeY, treeW;
        // Which folders are open (kept while VNyan runs; a rename keeps it).
        readonly Dictionary<ChainGroupConfig, bool> treeOpen = new Dictionary<ChainGroupConfig, bool>();

        static readonly Color TREE_ROW = new Color(0.20f, 0.20f, 0.24f, 0.0f);
        static readonly Color TREE_SEL = new Color(0.22f, 0.42f, 0.28f, 0.95f);
        static readonly Color TREE_FOLDER = new Color(1f, 0.86f, 0.55f, 1f);
        static readonly Color TREE_ITEM = new Color(0.88f, 0.91f, 1f, 1f);

        void SetupTree()
        {
            if (window == null || chainDropdown == null) return;
            RectTransform wrt = window.GetComponent<RectTransform>();
            RectTransform drt = chainDropdown.GetComponent<RectTransform>();
            if (wrt == null || drt == null) return;

            // Where the dropdown sits, measured from the window's top-left corner.
            Vector3[] c = new Vector3[4];
            drt.GetWorldCorners(c);
            Vector3 tl = wrt.InverseTransformPoint(c[1]);
            Vector3 br = wrt.InverseTransformPoint(c[3]);
            Rect wr = wrt.rect;
            treeX = tl.x - wr.xMin;
            treeY = (wr.yMax - br.y) + 2f;
            treeW = br.x - tl.x;

            // The Dropdown only shows the caption now: it must not pick by index or open its own
            // list from the keyboard (Submit would offer the one caption option).
            chainDropdown.onValueChanged.RemoveListener(OnChainSelected);
            Navigation nav = new Navigation();
            nav.mode = Navigation.Mode.None;
            chainDropdown.navigation = nav;

            // Clicking anywhere else in the window closes the tree.
            treeBlocker = new GameObject("Tree_Blocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform brt = treeBlocker.GetComponent<RectTransform>();
            brt.SetParent(window.transform, false);
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 1f);
            brt.offsetMin = new Vector2(0f, 0f);
            brt.offsetMax = new Vector2(0f, 0f);
            treeBlocker.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
            Button bb = treeBlocker.GetComponent<Button>();
            bb.transition = Selectable.Transition.None;
            bb.onClick.AddListener(CloseTree);
            treeBlocker.SetActive(false);

            treePanel = new GameObject("Tree_Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            PlaceAt(treePanel, window.transform, treeX, treeY, treeW, 200f);
            Image pimg = treePanel.GetComponent<Image>();
            Image wimg = window.GetComponent<Image>();
            if (wimg != null) { pimg.sprite = wimg.sprite; pimg.type = wimg.type; }
            pimg.color = new Color(0.10f, 0.10f, 0.12f, 0.99f);

            GameObject sc = new GameObject("Tree_Scroll", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
            RectTransform srt = sc.GetComponent<RectTransform>();
            srt.SetParent(treePanel.transform, false);
            srt.anchorMin = new Vector2(0f, 0f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.offsetMin = new Vector2(4f, 4f);
            srt.offsetMax = new Vector2(-4f, -4f);
            sc.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);

            GameObject vp = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
            RectTransform vrt = vp.GetComponent<RectTransform>();
            vrt.SetParent(srt, false);
            vrt.anchorMin = new Vector2(0f, 0f);
            vrt.anchorMax = new Vector2(1f, 1f);
            vrt.pivot = new Vector2(0f, 1f);
            vrt.offsetMin = new Vector2(0f, 0f);
            vrt.offsetMax = new Vector2(0f, 0f);
            vp.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);

            GameObject ct = new GameObject("Content", typeof(RectTransform));
            treeContent = ct.GetComponent<RectTransform>();
            treeContent.SetParent(vrt, false);
            treeContent.anchorMin = new Vector2(0f, 1f);
            treeContent.anchorMax = new Vector2(1f, 1f);
            treeContent.pivot = new Vector2(0f, 1f);
            treeContent.offsetMin = new Vector2(0f, 0f);
            treeContent.offsetMax = new Vector2(0f, 0f);
            treeContent.sizeDelta = new Vector2(0f, 0f);

            treeScroll = sc.GetComponent<ScrollRect>();
            treeScroll.content = treeContent;
            treeScroll.viewport = vrt;
            treeScroll.horizontal = false;
            treeScroll.vertical = true;
            treeScroll.movementType = ScrollRect.MovementType.Clamped;
            treeScroll.scrollSensitivity = 22f;

            // A clear button over the dropdown: clicks open the tree, never Unity's own list.
            GameObject ov = new GameObject("Tree_Opener", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform ort = ov.GetComponent<RectTransform>();
            ort.SetParent(drt, false);
            ort.anchorMin = new Vector2(0f, 0f);
            ort.anchorMax = new Vector2(1f, 1f);
            ort.offsetMin = new Vector2(0f, 0f);
            ort.offsetMax = new Vector2(0f, 0f);
            ov.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            Button ob = ov.GetComponent<Button>();
            ob.transition = Selectable.Transition.None;
            ob.onClick.AddListener(ToggleTree);

            treePanel.SetActive(false);
            UpdateTreeCaption();
        }

        void ToggleTree()
        {
            if (treePanel == null) return;
            if (treePanel.activeSelf) { CloseTree(); return; }
            treeBlocker.SetActive(true);
            treeBlocker.transform.SetAsLastSibling();
            treePanel.SetActive(true);
            treePanel.transform.SetAsLastSibling();
            // A group that is being edited, or holds the shown chain, starts open.
            if (editGroup != null) treeOpen[editGroup] = true;
            else if (selectedChain != null && config != null && config.groups != null)
                foreach (ChainGroupConfig g in config.groups)
                    if (g != null && InGroup(g, selectedChain)) treeOpen[g] = true;
            RebuildTreeRows(true);
        }

        void CloseTree()
        {
            if (treePanel != null) treePanel.SetActive(false);
            if (treeBlocker != null) treeBlocker.SetActive(false);
        }

        bool TreeIsOpen(ChainGroupConfig g)
        {
            bool open;
            return g != null && treeOpen.TryGetValue(g, out open) && open;
        }

        void RebuildTreeRows(bool scrollToSelection)
        {
            if (treeContent == null || treePanel == null || !treePanel.activeSelf || config == null) return;
            float keepY = treeContent.anchoredPosition.y;
            for (int i = treeContent.childCount - 1; i >= 0; i--)
                Destroy(treeContent.GetChild(i).gameObject);

            float w = treeW - 8f;
            float y = 0f, selY = -1f;
            List<ChainGroupConfig> groups = config.groups ?? new List<ChainGroupConfig>();
            HashSet<ChainConfig> grouped = new HashSet<ChainConfig>();

            for (int gi = 0; gi < groups.Count; gi++)
            {
                ChainGroupConfig g = groups[gi];
                if (g == null) continue;
                List<ChainConfig> mem = MembersOf(g);
                foreach (ChainConfig m in mem) grouped.Add(m);
                bool open = TreeIsOpen(g);
                bool gSel = editGroup == g;
                if (gSel) selY = y;

                ChainGroupConfig cg = g;
                GameObject row = TreeRow(y, w, gSel);
                TreeButton(row.transform, open ? "[-]" : "[+]", 0f, 30f, TREE_FOLDER, FontStyle.Bold,
                    delegate { ToggleFolder(cg); });
                TreeButton(row.transform, (string.IsNullOrEmpty(g.name) ? "group" : g.name) + "   (" + mem.Count + ")",
                    30f, w - 30f, TREE_FOLDER, FontStyle.Bold, delegate { PickGroup(cg); });
                y += TREE_ROW_H;

                if (!open) continue;
                foreach (ChainConfig b in mem)
                {
                    ChainConfig cb = b;
                    bool bSel = editGroup == null && selectedChain == b;
                    if (bSel && selY < 0f) selY = y;
                    GameObject r = TreeRow(y, w, bSel);
                    TreeButton(r.transform, b.name, TREE_INDENT + 12f, w - TREE_INDENT - 12f, TREE_ITEM, FontStyle.Normal,
                        delegate { PickChain(cb); });
                    y += TREE_ROW_H;
                }
            }

            bool headed = false;
            if (config.chains != null)
                foreach (ChainConfig b in config.chains)
                {
                    if (b == null || grouped.Contains(b)) continue;
                    if (groups.Count > 0 && !headed)
                    {
                        MakeLabel(treeContent, "Tree_Loose", "not in a group", 6f, y + 2f, w - 6f, TREE_ROW_H - 4f, 11,
                            TextAnchor.MiddleLeft, FontStyle.Italic).color = new Color(0.7f, 0.72f, 0.78f, 1f);
                        y += TREE_ROW_H;
                        headed = true;
                    }
                    ChainConfig cb = b;
                    bool bSel = editGroup == null && selectedChain == b;
                    if (bSel) selY = y;
                    GameObject r = TreeRow(y, w, bSel);
                    TreeButton(r.transform, b.name, 12f, w - 12f, TREE_ITEM, FontStyle.Normal, delegate { PickChain(cb); });
                    y += TREE_ROW_H;
                }
            if (y == 0f)
            {
                MakeLabel(treeContent, "Tree_Empty", "no chains yet: press Bones to add one", 6f, 2f, w - 6f, TREE_ROW_H - 4f,
                    12, TextAnchor.MiddleLeft, FontStyle.Italic);
                y = TREE_ROW_H;
            }

            treeContent.sizeDelta = new Vector2(0f, y);
            float h = Mathf.Min(TREE_MAX_H, y + 8f);
            RectTransform prt = treePanel.GetComponent<RectTransform>();
            prt.sizeDelta = new Vector2(treeW, h);

            // Keep the scroll where it was (opening or shutting a folder), or show the selection.
            float view = h - 8f;
            float maxY = Mathf.Max(0f, y - view);
            float want = keepY;
            if (scrollToSelection && selY >= 0f) want = Mathf.Max(0f, selY - view * 0.4f);
            treeContent.anchoredPosition = new Vector2(0f, Mathf.Clamp(want, 0f, maxY));
        }

        GameObject TreeRow(float y, float w, bool highlighted)
        {
            GameObject row = new GameObject("Tree_Row", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            PlaceAt(row, treeContent, 0f, y, w, TREE_ROW_H - 1f);
            Image img = row.GetComponent<Image>();
            img.color = highlighted ? TREE_SEL : TREE_ROW;
            img.raycastTarget = false;
            return row;
        }

        void TreeButton(Transform row, string label, float x, float w, Color color, FontStyle style,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject("Tree_Btn", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            PlaceAt(go, row, x, 0f, w, TREE_ROW_H - 1f);
            Image img = go.GetComponent<Image>();
            img.color = Color.white;
            Button b = go.GetComponent<Button>();
            b.targetGraphic = img;
            ColorBlock cb = b.colors;
            cb.normalColor = new Color(1f, 1f, 1f, 0f);
            cb.selectedColor = new Color(1f, 1f, 1f, 0f);
            cb.disabledColor = new Color(1f, 1f, 1f, 0f);
            cb.highlightedColor = new Color(1f, 1f, 1f, 0.12f);
            cb.pressedColor = new Color(1f, 1f, 1f, 0.2f);
            b.colors = cb;
            b.onClick.AddListener(onClick);
            Text t = MakeLabel(go.transform, "Text", label, 4f, 0f, w - 6f, TREE_ROW_H - 1f, 13, TextAnchor.MiddleLeft, style);
            t.color = color;
        }

        void ToggleFolder(ChainGroupConfig g)
        {
            if (g == null) return;
            treeOpen[g] = !TreeIsOpen(g);
            RebuildTreeRows(false);
        }

        void PickGroup(ChainGroupConfig g)
        {
            CloseTree();
            SelectGroupEdit(g);
        }

        void PickChain(ChainConfig b)
        {
            CloseTree();
            int i = config.chains.IndexOf(b);
            if (i >= 0) SelectChain(i);
        }

        // Edit a whole group from the top picker: the sliders show its first chain, and every
        // change goes to all of its chains.
        void SelectGroupEdit(ChainGroupConfig g)
        {
            if (config == null || g == null) return;
            editGroup = g;
            int gi = Groups().IndexOf(g);
            if (gi >= 0) groupIndex = gi;
            List<ChainConfig> mem = MembersOf(g);
            selectedChain = mem.Count > 0 ? mem[0] : null;
            if (selectedChain != null) PushChainToUI();
            else RefreshGroupUI();
            SetStatus(mem.Count > 0
                ? "editing group '" + g.name + "': the sliders change all " + mem.Count + " of its chains (showing '" + selectedChain.name + "')"
                : "group '" + g.name + "' has no chains yet: press Pick chains... below");
        }

        // Everyone an edit to the shown chain also reaches: its linked groups, plus the whole
        // group when a group was picked at the top.
        List<ChainConfig> EditPeers()
        {
            List<ChainConfig> res = LinkedPeers(selectedChain);
            if (editGroup != null)
                foreach (ChainConfig b in MembersOf(editGroup))
                    if (b != selectedChain && !res.Contains(b)) res.Add(b);
            return res;
        }

        // The text in the closed picker box.
        void UpdateTreeCaption()
        {
            if (chainDropdown == null) return;
            string label;
            if (editGroup != null)
                label = "[group] " + editGroup.name + "   (" + MembersOf(editGroup).Count + ")";
            else if (selectedChain != null)
                label = selectedChain.name;
            else
                label = (config != null && config.chains != null && config.chains.Count > 0) ? "(pick a chain)" : "(no chains yet)";
            bool was = suppressCallbacks;
            suppressCallbacks = true;
            chainDropdown.ClearOptions();
            chainDropdown.AddOptions(new List<string> { label });
            chainDropdown.value = 0;
            chainDropdown.RefreshShownValue();
            suppressCallbacks = was;
        }

        // After groups change: forget a deleted group, keep the shown chain inside the edited group.
        void SyncEditGroup()
        {
            if (editGroup == null || config == null) return;
            if (config.groups == null || !config.groups.Contains(editGroup)) { editGroup = null; return; }
            List<ChainConfig> mem = MembersOf(editGroup);
            if (mem.Count == 0) { selectedChain = null; return; }
            if (!mem.Contains(selectedChain))
            {
                selectedChain = mem[0];
                PushChainToUI();
            }
        }
    }
}

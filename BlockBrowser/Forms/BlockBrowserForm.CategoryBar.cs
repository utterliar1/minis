using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BlockBrowser
{
    public partial class BlockBrowserForm
    {
        private void RefreshCategories()
        {
            var categories = BlockLibrary.GetBrowsableCategories();
            _catBar.SuspendLayout();
            _catBar.Controls.Clear();
            foreach (var cat in categories)
            {
                var btn = new Button
                {
                    Text = cat, FlatStyle = FlatStyle.System, AutoSize = true,
                    MinimumSize = new Size(0, 24), Padding = new Padding(8, 1, 8, 1),
                    Margin = new Padding(2, 0, 2, 0), Font = new Font("Microsoft YaHei", 9f),
                    Tag = cat, Cursor = Cursors.Hand
                };
                btn.FlatAppearance.BorderSize = 1;
                SetCatBtnStyle(btn, cat == _currentCategory);
                btn.Click += (s, e) =>
                {
                    _currentCategory = (string)((Button)s).Tag;
                    _lastCategory = _currentCategory;
                    LoadBlocks();
                    UpdateCatHighlight();
                };
                _catBar.Controls.Add(btn);
            }
            _catBar.ResumeLayout();
            UpdateCategoryScroll();
        }

        private void SetCatBtnStyle(Button btn, bool active)
        {
            btn.FlatStyle = FlatStyle.System;
            // System style uses native rendering; use font bold for active highlight
            btn.Font = new Font("Microsoft YaHei", 9f, active ? FontStyle.Bold : FontStyle.Regular);
        }

        private void UpdateCatHighlight()
        {
            foreach (Control c in _catBar.Controls)
            {
                var btn = c as Button;
                if (btn != null) SetCatBtnStyle(btn, (string)btn.Tag == _currentCategory);
            }
        }

        private void UpdateCategoryScroll()
        {
            if (_catViewport == null || _catBar == null || _catScrollBar == null) return;

            _catBar.PerformLayout();
            int viewportWidth = Math.Max(0, _catViewport.ClientSize.Width);
            int viewportHeight = Math.Max(1, _catViewport.ClientSize.Height);
            int contentWidth = Math.Max(viewportWidth, _catBar.PreferredSize.Width);
            bool needsScroll = contentWidth > viewportWidth;

            _catBar.Height = viewportHeight;
            _catBar.Width = contentWidth;
            _catHost.Height = needsScroll ? 44 + 8 + SystemInformation.HorizontalScrollBarHeight : 44;
            _catLayout.RowStyles[1].Height = needsScroll ? 8 : 0;
            _catLayout.RowStyles[2].Height = needsScroll ? SystemInformation.HorizontalScrollBarHeight : 0;

            if (_catScrollBar.Visible != needsScroll)
            {
                _catScrollBar.Visible = needsScroll;
            }

            if (!needsScroll)
            {
                _catBar.Left = 0;
                _catScrollBar.Value = 0;
                _catScrollBar.Maximum = 0;
                return;
            }

            int maxValue = Math.Max(0, contentWidth - viewportWidth);
            _catScrollBar.SmallChange = 32;
            _catScrollBar.LargeChange = Math.Max(1, viewportWidth);
            _catScrollBar.Maximum = maxValue + _catScrollBar.LargeChange - 1;
            if (_catScrollBar.Value > maxValue) _catScrollBar.Value = maxValue;
            _catBar.Left = -_catScrollBar.Value;
        }
        private void BtnCreateCategory_Click(object sender, EventArgs e)
        {
            string category = ShowInputDialog("输入新分类名称:");
            if (string.IsNullOrEmpty(category)) return;

            try
            {
                var result = BlockLibrary.CreateCategory(category);
                if (!result.IsValid)
                {
                    MessageBox.Show("分类名称为空或包含非法字符。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                RefreshCategories();
                _lblStatus.Text = result.Created
                    ? "已创建分类: " + result.Category
                    : "分类已存在: " + result.Category;
            }
            catch (Exception ex)
            {
                MessageBox.Show("创建分类失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

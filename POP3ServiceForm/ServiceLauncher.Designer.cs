/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

namespace Pop3ServiceForm
{
    partial class ServiceLauncher
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.addMessageCmd = new System.Windows.Forms.Button();
            this.messageList = new System.Windows.Forms.DataGridView();
            this.label3 = new System.Windows.Forms.Label();
            this.usersList = new System.Windows.Forms.ListBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.addUserCmd = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.addUserPassTextBox = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.addUserNameTextBox = new System.Windows.Forms.TextBox();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.uniqueIDCol = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.fromCol = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.toCol = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.subjectCol = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.bodyCol = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.tabControl1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.messageList)).BeginInit();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // listBox1
            // 
            this.listBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBox1.FormattingEnabled = true;
            this.listBox1.Location = new System.Drawing.Point(0, 0);
            this.listBox1.Name = "listBox1";
            this.listBox1.SelectionMode = System.Windows.Forms.SelectionMode.None;
            this.listBox1.Size = new System.Drawing.Size(1123, 186);
            this.listBox1.TabIndex = 2;
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(1123, 522);
            this.tabControl1.TabIndex = 3;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.addMessageCmd);
            this.tabPage2.Controls.Add(this.messageList);
            this.tabPage2.Controls.Add(this.label3);
            this.tabPage2.Controls.Add(this.usersList);
            this.tabPage2.Controls.Add(this.groupBox1);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(1115, 496);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Users";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // addMessageCmd
            // 
            this.addMessageCmd.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.addMessageCmd.Location = new System.Drawing.Point(1031, 458);
            this.addMessageCmd.Name = "addMessageCmd";
            this.addMessageCmd.Size = new System.Drawing.Size(75, 23);
            this.addMessageCmd.TabIndex = 4;
            this.addMessageCmd.Text = "Add";
            this.addMessageCmd.UseVisualStyleBackColor = true;
            this.addMessageCmd.Click += new System.EventHandler(this.addMessageCmd_Click);
            // 
            // messageList
            // 
            this.messageList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.messageList.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.messageList.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.uniqueIDCol,
            this.fromCol,
            this.toCol,
            this.subjectCol,
            this.bodyCol});
            this.messageList.Location = new System.Drawing.Point(326, 20);
            this.messageList.Name = "messageList";
            this.messageList.Size = new System.Drawing.Size(781, 431);
            this.messageList.TabIndex = 3;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(15, 7);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(37, 13);
            this.label3.TabIndex = 2;
            this.label3.Text = "Users:";
            // 
            // usersList
            // 
            this.usersList.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.usersList.FormattingEnabled = true;
            this.usersList.Location = new System.Drawing.Point(15, 20);
            this.usersList.Name = "usersList";
            this.usersList.Size = new System.Drawing.Size(304, 355);
            this.usersList.TabIndex = 1;
            this.usersList.SelectedIndexChanged += new System.EventHandler(this.usersList_SelectedIndexChanged);
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.groupBox1.Controls.Add(this.addUserCmd);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.addUserPassTextBox);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.addUserNameTextBox);
            this.groupBox1.Location = new System.Drawing.Point(6, 385);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(313, 105);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Add User";
            // 
            // addUserCmd
            // 
            this.addUserCmd.Enabled = false;
            this.addUserCmd.Location = new System.Drawing.Point(228, 73);
            this.addUserCmd.Name = "addUserCmd";
            this.addUserCmd.Size = new System.Drawing.Size(75, 23);
            this.addUserCmd.TabIndex = 4;
            this.addUserCmd.Text = "Add";
            this.addUserCmd.UseVisualStyleBackColor = true;
            this.addUserCmd.Click += new System.EventHandler(this.addUserCmd_Click);
            // 
            // label2
            // 
            this.label2.Location = new System.Drawing.Point(6, 46);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(60, 20);
            this.label2.TabIndex = 3;
            this.label2.Text = "Password:";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // addUserPassTextBox
            // 
            this.addUserPassTextBox.Location = new System.Drawing.Point(72, 46);
            this.addUserPassTextBox.Name = "addUserPassTextBox";
            this.addUserPassTextBox.Size = new System.Drawing.Size(232, 20);
            this.addUserPassTextBox.TabIndex = 2;
            this.addUserPassTextBox.TextChanged += new System.EventHandler(this.addUserNameTextBox_TextChanged);
            // 
            // label1
            // 
            this.label1.Location = new System.Drawing.Point(6, 20);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(60, 20);
            this.label1.TabIndex = 1;
            this.label1.Text = "Name:";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // addUserNameTextBox
            // 
            this.addUserNameTextBox.Location = new System.Drawing.Point(72, 20);
            this.addUserNameTextBox.Name = "addUserNameTextBox";
            this.addUserNameTextBox.Size = new System.Drawing.Size(232, 20);
            this.addUserNameTextBox.TabIndex = 0;
            this.addUserNameTextBox.TextChanged += new System.EventHandler(this.addUserNameTextBox_TextChanged);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.tabControl1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.listBox1);
            this.splitContainer1.Size = new System.Drawing.Size(1123, 712);
            this.splitContainer1.SplitterDistance = 522;
            this.splitContainer1.TabIndex = 4;
            // 
            // uniqueIDCol
            // 
            this.uniqueIDCol.HeaderText = "UniqueID";
            this.uniqueIDCol.Name = "uniqueIDCol";
            this.uniqueIDCol.ReadOnly = true;
            // 
            // fromCol
            // 
            this.fromCol.HeaderText = "From";
            this.fromCol.Name = "fromCol";
            this.fromCol.ReadOnly = true;
            // 
            // toCol
            // 
            this.toCol.HeaderText = "To";
            this.toCol.Name = "toCol";
            this.toCol.ReadOnly = true;
            // 
            // subjectCol
            // 
            this.subjectCol.HeaderText = "Subject";
            this.subjectCol.Name = "subjectCol";
            this.subjectCol.ReadOnly = true;
            // 
            // bodyCol
            // 
            this.bodyCol.HeaderText = "Body";
            this.bodyCol.Name = "bodyCol";
            this.bodyCol.ReadOnly = true;
            // 
            // ServiceLauncher
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1123, 712);
            this.Controls.Add(this.splitContainer1);
            this.Name = "ServiceLauncher";
            this.Text = "POP3 Service Launcher";
            this.Load += new System.EventHandler(this.ServiceLauncher_Load);
            this.tabControl1.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            this.tabPage2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.messageList)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.ListBox listBox1;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Button addMessageCmd;
        private System.Windows.Forms.DataGridView messageList;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ListBox usersList;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button addUserCmd;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox addUserPassTextBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox addUserNameTextBox;
        private System.Windows.Forms.DataGridViewTextBoxColumn uniqueIDCol;
        private System.Windows.Forms.DataGridViewTextBoxColumn fromCol;
        private System.Windows.Forms.DataGridViewTextBoxColumn toCol;
        private System.Windows.Forms.DataGridViewTextBoxColumn subjectCol;
        private System.Windows.Forms.DataGridViewTextBoxColumn bodyCol;
    }
}



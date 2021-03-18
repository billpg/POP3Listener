/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

namespace Pop3ServiceForm
{
    partial class MessageContentsForm
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
            this.uniqueIdTextBox = new System.Windows.Forms.TextBox();
            this.uswerLabel = new System.Windows.Forms.Label();
            this.fromTextBox = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.contentsTextBox = new System.Windows.Forms.TextBox();
            this.submitCmd = new System.Windows.Forms.Button();
            this.subjectTextBox = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.toTextBox = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.populateCmd = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // uniqueIdTextBox
            // 
            this.uniqueIdTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.uniqueIdTextBox.Location = new System.Drawing.Point(85, 12);
            this.uniqueIdTextBox.Name = "uniqueIdTextBox";
            this.uniqueIdTextBox.Size = new System.Drawing.Size(703, 20);
            this.uniqueIdTextBox.TabIndex = 9;
            // 
            // uswerLabel
            // 
            this.uswerLabel.Location = new System.Drawing.Point(12, 12);
            this.uswerLabel.Name = "uswerLabel";
            this.uswerLabel.Size = new System.Drawing.Size(67, 20);
            this.uswerLabel.TabIndex = 8;
            this.uswerLabel.Text = "Unique ID:";
            this.uswerLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // fromTextBox
            // 
            this.fromTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.fromTextBox.Location = new System.Drawing.Point(85, 38);
            this.fromTextBox.Name = "fromTextBox";
            this.fromTextBox.Size = new System.Drawing.Size(703, 20);
            this.fromTextBox.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.Location = new System.Drawing.Point(-3, 38);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(82, 20);
            this.label1.TabIndex = 0;
            this.label1.Text = "From:";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // contentsTextBox
            // 
            this.contentsTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.contentsTextBox.Location = new System.Drawing.Point(15, 116);
            this.contentsTextBox.Multiline = true;
            this.contentsTextBox.Name = "contentsTextBox";
            this.contentsTextBox.Size = new System.Drawing.Size(773, 293);
            this.contentsTextBox.TabIndex = 6;
            // 
            // submitCmd
            // 
            this.submitCmd.Location = new System.Drawing.Point(712, 415);
            this.submitCmd.Name = "submitCmd";
            this.submitCmd.Size = new System.Drawing.Size(75, 23);
            this.submitCmd.TabIndex = 7;
            this.submitCmd.Text = "Save";
            this.submitCmd.UseVisualStyleBackColor = true;
            this.submitCmd.Click += new System.EventHandler(this.submitCmd_Click);
            // 
            // subjectTextBox
            // 
            this.subjectTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.subjectTextBox.Location = new System.Drawing.Point(85, 90);
            this.subjectTextBox.Name = "subjectTextBox";
            this.subjectTextBox.Size = new System.Drawing.Size(703, 20);
            this.subjectTextBox.TabIndex = 5;
            // 
            // label2
            // 
            this.label2.Location = new System.Drawing.Point(-3, 90);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(82, 20);
            this.label2.TabIndex = 4;
            this.label2.Text = "Subject";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // toTextBox
            // 
            this.toTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.toTextBox.Location = new System.Drawing.Point(85, 64);
            this.toTextBox.Name = "toTextBox";
            this.toTextBox.Size = new System.Drawing.Size(703, 20);
            this.toTextBox.TabIndex = 3;
            // 
            // label3
            // 
            this.label3.Location = new System.Drawing.Point(12, 64);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(67, 20);
            this.label3.TabIndex = 2;
            this.label3.Text = "To:";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // populateCmd
            // 
            this.populateCmd.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.populateCmd.Location = new System.Drawing.Point(631, 416);
            this.populateCmd.Name = "populateCmd";
            this.populateCmd.Size = new System.Drawing.Size(75, 23);
            this.populateCmd.TabIndex = 10;
            this.populateCmd.Text = "Populate";
            this.populateCmd.UseVisualStyleBackColor = true;
            this.populateCmd.Click += new System.EventHandler(this.populateCmd_Click);
            // 
            // MessageContentsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.populateCmd);
            this.Controls.Add(this.subjectTextBox);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.toTextBox);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.submitCmd);
            this.Controls.Add(this.contentsTextBox);
            this.Controls.Add(this.fromTextBox);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.uniqueIdTextBox);
            this.Controls.Add(this.uswerLabel);
            this.Name = "MessageContentsForm";
            this.Text = "Add/Edit Message";
            this.Load += new System.EventHandler(this.MessageContentsForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox uniqueIdTextBox;
        private System.Windows.Forms.Label uswerLabel;
        private System.Windows.Forms.TextBox fromTextBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox contentsTextBox;
        private System.Windows.Forms.Button submitCmd;
        private System.Windows.Forms.TextBox subjectTextBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox toTextBox;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button populateCmd;
    }
}

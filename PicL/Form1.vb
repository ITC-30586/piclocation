'
'
'
Imports System.IO.Ports
Imports System.Management
Imports System.Reflection.Emit
Imports System.Windows.Forms.VisualStyles.VisualStyleElement
Imports System.Windows.Forms.VisualStyles.VisualStyleElement.ToolBar
Imports C1.Framework.Extension
Imports C1.Util.Win.Win32
Imports C1.Win.C1FlexGrid
Imports C1.Win.TouchToolKit
Imports Npgsql
Imports System.Text

Public Class Form1

    ' 定数定義
    Private Const GRID_SIZE As Integer = 100
    Private Const CELL_HEIGHT As Integer = 9
    Private Const CELL_WIDTH As Integer = 15
    Private Const APP_VERSION As String = "Ver1.0.0"
    Private Const FONT_SIZE As Single = 6
    Private Const FONT_NAME As String = "ＭＳ 明朝"

    ' ComboBoxインデックス定数
    Private Const COMBO_INDEX_NONE As Integer = 0
    Private Const COMBO_INDEX_CLEAR As Integer = 1
    Private Const COMBO_INDEX_STYLE1 As Integer = 2
    Private Const COMBO_INDEX_STYLE2 As Integer = 3
    Private Const COMBO_INDEX_STYLE3 As Integer = 4
    Private Const COMBO_INDEX_STYLE4 As Integer = 5
    Private Const COMBO_INDEX_STYLE5 As Integer = 6

    ' セルスタイル名定数
    Private Const STYLE_NS1 As String = "NS1"
    Private Const STYLE_NS2 As String = "NS2"
    Private Const STYLE_NS3 As String = "NS3"
    Private Const STYLE_NS4 As String = "NS4"
    Private Const STYLE_NS5 As String = "NS5"

    ' フィールド
    Private stCurrentDir As String
    Private styleNS1 As CellStyle
    Private styleNS2 As CellStyle
    Private styleNS3 As CellStyle
    Private styleNS4 As CellStyle
    Private styleNS5 As CellStyle

    ' データベース接続情報
    Private dbIPAddress As String
    Private dbPort As String
    Private dbName As String
    Private selectedComboIndex As Integer = COMBO_INDEX_NONE

    Private ReadOnly cn1 As New NpgsqlConnection
    Private ReadOnly cn2 As New NpgsqlConnection

    '処理用変数
    Public DP2 As String = "BH010042617009101ZZ730225CQ64Q8"
    Public PR1 As String

    ' シリアルポート
    Private WithEvents _port As SerialPort

    'SerialPort受信バッファ
    Dim blockbuf As String

    ' QRコード分解用構造体　
    Private Structure QrCodeParts
        Public Property Raw As String

        Public Property Hinban As String    ' 1～4 品番
        Public Property Kkyoten As String   ' 5 刻印拠点
        Public Property Line As String      ' 6～7 刻印ライン
        Public Property Kyear As String     ' 8～9 刻印年
        Public Property Kmonth As String    ' 10 刻印月
        Public Property Kday As String      ' 11 刻印日
        Public Property Sserial As String   ' 12～16 シリアル仕上数
        Public Property Buhin As String     ' 17 部品情報 MH:1 RO:2 FH:3 RH:4 SH:5 
        Public Property Dcline As String    ' 20-21 DCライン
        Public Property Kata As String      ' 22-23 金型
        Public Property Cyear As String     ' 24～25 鋳造年
        Public Property Cmonth As String    ' 26 鋳造月
        Public Property Cday As String      ' 27 鋳造日
        Public Property Ctime As String     ' 28 鋳造時間
        Public Property Nro As String       ' 29 熱処理炉    
        Public Property Nday As String      ' 30 熱処理日
        Public Property Ntime As String     ' 31 熱処理時間

        Public Shared Function TryParse(raw As String, ByRef result As QrCodeParts) As Boolean
            result = Nothing

            If String.IsNullOrWhiteSpace(raw) OrElse raw.Length < 7 Then
                Return False
            End If

            result = New QrCodeParts With {
                .Raw = raw,
                .Hinban = raw.Substring(0, 4),
                .Kkyoten = raw.Substring(4, 1),
                .Line = raw.Substring(5, 2),
                .Kyear = raw.Substring(7, 2),
                .Kmonth = raw.Substring(9, 1),
                .Kday = raw.Substring(10, 1),
                .Sserial = raw.Substring(11, 5),
                .Buhin = raw.Substring(16, 1),
                .Dcline = raw.Substring(19, 2),
                .Kata = raw.Substring(21, 2),
                .Cyear = raw.Substring(23, 2),
                .Cmonth = raw.Substring(25, 1),
                .Cday = raw.Substring(26, 1),
                .Ctime = raw.Substring(27, 1),
                .Nro = raw.Substring(28, 1),
                .Nday = raw.Substring(29, 1),
                .Ntime = raw.Substring(30, 1)
            }

            Return True
        End Function
    End Structure






    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles Me.Load
        Try
            ' 2重起動防止
            If IsApplicationAlreadyRunning() Then
                MessageBox.Show("アプリケーションは既に起動しています。", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Me.Close()
                Return
            End If

            ' 起動パス
            stCurrentDir = System.IO.Directory.GetCurrentDirectory()

            ' 初期化処理
            LoadConfiguration()
            ConnectToDatabase()
            InitializeForm()
            InitializeGrid()
            InitializeGrid2()
            InitializeCellStyles()

            ' シリアルポートオーブン
            OpenQrPort()



        Catch ex As IO.FileNotFoundException
            WriteErrorLog("Form1_Load", ex.Message)
            MessageBox.Show($"必要なファイルが見つかりません: {ex.FileName}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Me.Close()
        Catch ex As NpgsqlException
            WriteErrorLog("Form1_Load", ex.Message)
            MessageBox.Show("データベース接続エラーが発生しました。設定を確認してください。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Me.Close()
        Catch ex As Exception
            WriteErrorLog("Form1_Load", ex.Message)
            MessageBox.Show("初期化中にエラーが発生しました。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Me.Close()
        End Try
    End Sub

    ' 2重起動チェック
    Private Function IsApplicationAlreadyRunning() As Boolean
        Dim processName As String = Process.GetCurrentProcess.ProcessName
        Return Process.GetProcessesByName(processName).Length > 1
    End Function

    ' 設定ファイル読み込み (旧:Initialization)
    Private Sub LoadConfiguration()
        Try
            Dim configPath As String = System.IO.Path.Combine(My.Application.Info.DirectoryPath, "INI.xml")

            If Not System.IO.File.Exists(configPath) Then
                Throw New IO.FileNotFoundException("設定ファイルが見つかりません。", configPath)
            End If

            ' XMLファイルの読み込み
            Dim xmlDoc As New System.Xml.XmlDocument
            xmlDoc.Load(configPath)
            Dim rootElement As System.Xml.XmlElement = xmlDoc.DocumentElement

            If rootElement Is Nothing Then
                Throw New System.Xml.XmlException("XMLファイルのルート要素が見つかりません。")
            End If

            ' DB設定取得
            dbIPAddress = GetXmlNodeValue(rootElement, "db_ip", "133.222.186.64")
            dbPort = GetXmlNodeValue(rootElement, "db_port", "25432")
            dbName = GetXmlNodeValue(rootElement, "db_name", "dbpiclocation")

        Catch ex As Exception
            WriteErrorLog("LoadConfiguration", ex.Message)
            Throw
        End Try
    End Sub

    ' XMLノード値取得ヘルパー
    Private Function GetXmlNodeValue(rootElement As System.Xml.XmlElement, tagName As String, defaultValue As String) As String
        Dim nodes As System.Xml.XmlNodeList = rootElement.GetElementsByTagName(tagName)
        If nodes.Count > 0 AndAlso Not String.IsNullOrWhiteSpace(nodes.Item(0).InnerText) Then
            Return nodes.Item(0).InnerText.Trim()
        End If
        Return defaultValue
    End Function

    ' データベース接続 (旧:DB_Connect)
    Private Sub ConnectToDatabase()
        Try
            Dim connectionString As String = BuildConnectionString()
            cn1.ConnectionString = connectionString
            cn2.ConnectionString = connectionString

            ' 接続テスト
            cn1.Open()
            cn1.Close()

        Catch ex As NpgsqlException
            WriteErrorLog("ConnectToDatabase", ex.Message)
            Throw
        Catch ex As Exception
            WriteErrorLog("ConnectToDatabase", ex.Message)
            Throw
        End Try
    End Sub

    ' 接続文字列構築
    Private Function BuildConnectionString() As String
        Return $"Server={dbIPAddress};Port={dbPort};User id=postgres;Password=postgres;Database={dbName};"
    End Function

    ' セルスタイルの初期化
    Private Sub InitializeCellStyles()
        ' 作業A用スタイル
        styleNS1 = CreateCellStyle(STYLE_NS1, System.Drawing.Color.FromArgb(50, 169, 169, 169), System.Drawing.Color.Crimson)

        ' 作業B用スタイル
        styleNS2 = CreateCellStyle(STYLE_NS2, System.Drawing.Color.FromArgb(50, 210, 180, 140), System.Drawing.Color.DodgerBlue)

        ' 作業C用スタイル
        styleNS3 = CreateCellStyle(STYLE_NS3, System.Drawing.Color.FromArgb(50, 135, 206, 235), System.Drawing.Color.Green)

        ' 作業D用スタイル
        styleNS4 = CreateCellStyle(STYLE_NS4, System.Drawing.Color.FromArgb(50, 255, 127, 80), System.Drawing.Color.SaddleBrown)

        ' 作業E用スタイル
        styleNS5 = CreateCellStyle(STYLE_NS5, System.Drawing.Color.FromArgb(50, 255, 215, 0), System.Drawing.Color.DarkOliveGreen)
    End Sub

    ' セルスタイル作成ヘルパー
    Private Function CreateCellStyle(styleName As String, backColor As System.Drawing.Color, foreColor As System.Drawing.Color) As CellStyle
        Dim style As CellStyle = C1FlexGrid1.Styles.Add(styleName)
        With style
            .BackColor = backColor
            .ForeColor = foreColor
            .Font = New Font(FONT_NAME, FONT_SIZE, FontStyle.Regular)
            .TextAlign = TextAlignEnum.CenterCenter
        End With
        Return style
    End Function

    ' エラーログを書き出す
    Private Sub WriteErrorLog(methodName As String, errorMessage As String)
        Try
            Dim logFilePath As String = System.IO.Path.Combine(stCurrentDir, "errlog.txt")
            Dim timestamp As String = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")
            Dim logEntry As String = $"[{timestamp}] {methodName}: {errorMessage}{Environment.NewLine}"

            System.IO.File.AppendAllText(logFilePath, logEntry, System.Text.Encoding.UTF8)
        Catch ex As Exception
            Debug.WriteLine($"エラーログ書き込み失敗: {ex.Message}")
        End Try
    End Sub

    ' 起動時画面設定
    Private Sub InitializeForm()
        Me.Text = $"PICロケーション作業記録 {APP_VERSION}"

        TabControl1.SizeMode = TabSizeMode.Fixed
        TabControl1.ItemSize = New Drawing.Size(100, 30)

        InitializeComboBox()

        '作業番号
        TextBox1.Text = ""


    End Sub

    ' グリッド初期化 (旧:Init_C1FG01)
    Private Sub InitializeGrid()
        Try
            C1FlexGrid1.Visible = False
            C1FlexGrid1.Location = New Drawing.Point(29, 24)
            C1FlexGrid1.Size = New Drawing.Size(1504, 906)
            C1FlexGrid1.Clear()

            ' 背景画像設定
            LoadBackgroundImage()

            ' グリッド設定
            ConfigureGridAppearance()
            ConfigureGridSize()

            C1FlexGrid1.Visible = True
            C1FlexGrid1.Select(-1, -1, True)

        Catch ex As Exception
            WriteErrorLog("InitializeGrid", ex.Message)
            Throw
        End Try
    End Sub

    Private Sub InitializeGrid2()
        Try
            C1FlexGrid2.Visible = False
            'C1FlexGrid2.Location = New Drawing.Point(2633, 553)
            'C1FlexGrid2.Size = New Drawing.Size(393, 329)
            C1FlexGrid2.Clear()


            ' グリッド設定
            '            ConfigureGridAppearance()
            '            ConfigureGridSize()

            With C1FlexGrid2
                .Rows.Fixed = 1
                .Cols.Fixed = 1

                '右端の処理
                .ExtendLastCol = True

                'filed数
                .Cols.Count = 4
                .Rows.Count = 1


                '初期表示
                .Cols(0).Caption = "NO"
                .Cols(1).Caption = "X座標"
                .Cols(2).Caption = "Y座標"
                .Cols(3).Caption = "不具合"

                '表示幅
                .Cols(0).Width = 40
                .Cols(1).Width = 60
                .Cols(2).Width = 60
                .Cols(3).Width = 60

                For i = 0 To 3
                    .Cols(i).TextAlignFixed = C1.Win.C1FlexGrid.TextAlignEnum.CenterCenter
                    .Cols(i).StyleFixed.Font = New Font("verda", 8, FontStyle.Regular)

                    .Cols(i).TextAlign = C1.Win.C1FlexGrid.TextAlignEnum.CenterCenter
                    .Cols(i).Style.Font = New Font("verda", 9, FontStyle.Regular)
                Next

            End With

            C1FlexGrid2.Visible = True
            C1FlexGrid2.Select(-1, -1, True)

        Catch ex As Exception
            WriteErrorLog("InitializeGrid2", ex.Message)
            Throw
        End Try
    End Sub









    ' 背景画像読み込み
    Private Sub LoadBackgroundImage()
        Dim imagePath As String = System.IO.Path.Combine(Application.StartupPath, "default.png")
        'Dim imagePath As String = System.IO.Path.Combine(Application.StartupPath, "Sample.png")
        If System.IO.File.Exists(imagePath) Then
            C1FlexGrid1.BackgroundImage = Image.FromFile(imagePath)
            C1FlexGrid1.BackgroundImageLayout = ImageLayout.Stretch
        End If
    End Sub

    ' グリッド外観設定
    Private Sub ConfigureGridAppearance()
        C1FlexGrid1.Cols(0).Visible = False
        C1FlexGrid1.Rows(0).Visible = False

        ' 罫線設定
        With C1FlexGrid1.Styles.Normal.Border
            .Color = System.Drawing.Color.Gray
            .Style = BorderStyleEnum.Dotted
            .Width = 1
        End With

        ' フォーカス罫線設定
        With C1FlexGrid1.Styles.Focus.Border
            .Color = System.Drawing.Color.Red
            .Style = BorderStyleEnum.Double
        End With
    End Sub

    ' グリッドサイズ設定
    Private Sub ConfigureGridSize()
        With C1FlexGrid1
            .Rows.DefaultSize = CELL_HEIGHT
            .Cols.DefaultSize = CELL_WIDTH
            .Rows.Fixed = 0
            .Cols.Fixed = 0
            .Cols.Count = GRID_SIZE + 1
            .Rows.Count = GRID_SIZE + 1
            .ScrollBars = ScrollBars.None

            For i As Integer = 0 To GRID_SIZE
                .Cols(i).Width = CELL_WIDTH
                .Rows(i).Height = CELL_HEIGHT
            Next
        End With
    End Sub

    ' セル選択イベント
    Private Sub C1FlexGrid1_Click(sender As Object, e As EventArgs) Handles C1FlexGrid1.Click
        Try
            Dim colIndex As Integer = C1FlexGrid1.Col
            Dim rowIndex As Integer = C1FlexGrid1.Row

            ' ComboBoxが選択されていない場合は処理無効
            If selectedComboIndex = COMBO_INDEX_NONE Then
                Return
            End If

            ' 無効な選択をチェック
            If rowIndex <= 0 OrElse colIndex < 0 Then
                Return
            End If

            ' ComboBoxの選択に応じてセルスタイルを変更
            ApplyCellStyle(rowIndex, colIndex, selectedComboIndex)

            ' 作業位置を表示
            TextBox2.Text = $"({colIndex},{rowIndex})"

            ' カレントセルのカーソルを非表示
            C1FlexGrid1.Row = -1
            C1FlexGrid1.Col = -1

            'DB保存処理をここに追加可能
            Select Case selectedComboIndex
                Case COMBO_INDEX_CLEAR
                    ' 保存処理クリア
                    Call DeleteWorkDataALL(PR1, colIndex, rowIndex)

                Case COMBO_INDEX_STYLE1
                    ' 保存処理A
                    Call InsertWorkData(PR1, colIndex, rowIndex, 1)
                Case COMBO_INDEX_STYLE2
                    ' 保存処理B
                    Call InsertWorkData(PR1, colIndex, rowIndex, 2)
                Case COMBO_INDEX_STYLE3
                    ' 保存処理C
                    Call InsertWorkData(PR1, colIndex, rowIndex, 3)
                Case COMBO_INDEX_STYLE4
                    ' 保存処理D
                    Call InsertWorkData(PR1, colIndex, rowIndex, 4)
                Case COMBO_INDEX_STYLE5
                    ' 保存処理E
                    Call InsertWorkData(PR1, colIndex, rowIndex, 5)
            End Select

            ' 作業データをc1flexgrid2に表示する
            DisplayWorkDataInGrid2(PR1)

        Catch ex As Exception
            WriteErrorLog("C1FlexGrid1_Click", ex.Message)
            MessageBox.Show("セル選択中にエラーが発生しました。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub


    '新規作業データ表示　c1flexgrid2にtemp_work02の作業データを表示する
    Private Sub DisplayWorkDataInGrid2_S(PicHinban As String)
        Try
            Dim pictureDir As String = System.IO.Path.Combine(Application.StartupPath, "picture")

            If Not System.IO.Directory.Exists(pictureDir) Then
                Exit Sub
            End If

            Dim targetFile As String = String.Empty
            Dim exts As String() = {".jpg", ".jpeg", ".png"}

            For Each ext As String In exts
                Dim filePath As String = System.IO.Path.Combine(pictureDir, PicHinban & ext)
                If System.IO.File.Exists(filePath) Then
                    targetFile = filePath
                    Exit For
                End If
            Next

            If String.IsNullOrEmpty(targetFile) Then
                Exit Sub
            End If

            Dim oldImage As Image = C1FlexGrid1.BackgroundImage

            Using fs As New System.IO.FileStream(targetFile, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite)
                Using img As Image = Image.FromStream(fs)
                    C1FlexGrid1.BackgroundImage = New Bitmap(img) ' ファイルロック回避



                End Using
            End Using

            C1FlexGrid1.BackgroundImageLayout = ImageLayout.Stretch

            If oldImage IsNot Nothing Then
                oldImage.Dispose()
            End If

        Catch ex As Exception
            WriteErrorLog("DisplayWorkDataInGrid2_S", ex.Message)
        End Try
    End Sub



    'c1flexgrid2にtemp_work02の作業データを表示する
    Private Sub DisplayWorkDataInGrid2(workno As String)
        Try
            Dim sql = "SELECT xp, yp, dno FROM temp_work02 WHERE workno=@workno ORDER BY xp, yp;"
            Using cn As New NpgsqlConnection(cn1.ConnectionString),
                cmd As New NpgsqlCommand(sql, cn)
                cmd.Parameters.AddWithValue("@workno", workno)
                cn.Open()
                Using reader As NpgsqlDataReader = cmd.ExecuteReader()
                    C1FlexGrid2.Rows.Count = 1 ' ヘッダー行のみ残す
                    Dim rowIndex As Integer = 1
                    While reader.Read()
                        C1FlexGrid2.Rows.Add()
                        C1FlexGrid2.SetData(rowIndex, 0, rowIndex) ' NO
                        C1FlexGrid2.SetData(rowIndex, 1, reader.GetInt32(0)) ' X座標
                        C1FlexGrid2.SetData(rowIndex, 2, reader.GetInt32(1)) ' Y座標
                        C1FlexGrid2.SetData(rowIndex, 3, reader.GetInt32(2)) ' 不具合番号
                        rowIndex += 1
                    End While
                End Using
                cn.Close()
            End Using
        Catch ex As Exception
            WriteErrorLog("DisplayWorkDataInGrid2", ex.Message)
            MessageBox.Show("作業データ表示中にエラーが発生しました。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    'c1flexgrid2にwork02の作業データを表示する
    Private Sub DisplayWorkDataInGrid2K(workno As String)
        Try
            Dim sql = "SELECT xp, yp, dno FROM work02 WHERE workno=@workno ORDER BY xp, yp;"
            Using cn As New NpgsqlConnection(cn1.ConnectionString),
                cmd As New NpgsqlCommand(sql, cn)
                cmd.Parameters.AddWithValue("@workno", workno)
                cn.Open()
                Using reader As NpgsqlDataReader = cmd.ExecuteReader()
                    C1FlexGrid2.Rows.Count = 1 ' ヘッダー行のみ残す
                    Dim rowIndex As Integer = 1
                    While reader.Read()
                        C1FlexGrid2.Rows.Add()
                        C1FlexGrid2.SetData(rowIndex, 0, rowIndex) ' NO
                        C1FlexGrid2.SetData(rowIndex, 1, reader.GetInt32(0)) ' X座標
                        C1FlexGrid2.SetData(rowIndex, 2, reader.GetInt32(1)) ' Y座標
                        C1FlexGrid2.SetData(rowIndex, 3, reader.GetInt32(2)) ' 不具合番号
                        rowIndex += 1
                    End While
                End Using
                cn.Close()
            End Using
        Catch ex As Exception
            WriteErrorLog("DisplayWorkDataInGrid2", ex.Message)
            MessageBox.Show("作業データ表示中にエラーが発生しました。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub







    ' セルスタイル適用
    Private Sub ApplyCellStyle(rowIndex As Integer, colIndex As Integer, comboIndex As Integer)
        Select Case comboIndex
            Case COMBO_INDEX_CLEAR
                C1FlexGrid1.SetData(rowIndex, colIndex, "")
            Case COMBO_INDEX_STYLE1
                C1FlexGrid1.SetCellStyle(rowIndex, colIndex, styleNS1)
                C1FlexGrid1.SetData(rowIndex, colIndex, "●")
            Case COMBO_INDEX_STYLE2
                C1FlexGrid1.SetCellStyle(rowIndex, colIndex, styleNS2)
                C1FlexGrid1.SetData(rowIndex, colIndex, "◆")
            Case COMBO_INDEX_STYLE3
                C1FlexGrid1.SetCellStyle(rowIndex, colIndex, styleNS3)
                C1FlexGrid1.SetData(rowIndex, colIndex, "■")
            Case COMBO_INDEX_STYLE4
                C1FlexGrid1.SetCellStyle(rowIndex, colIndex, styleNS4)
                C1FlexGrid1.SetData(rowIndex, colIndex, "▲")
            Case COMBO_INDEX_STYLE5
                C1FlexGrid1.SetCellStyle(rowIndex, colIndex, styleNS5)
                C1FlexGrid1.SetData(rowIndex, colIndex, "▼")
        End Select
    End Sub

    ' ComboBox初期化 (旧:Init_Combo01)
    Private Sub InitializeComboBox()

        With ComboBox1
            .Items.Clear()
            .Items.AddRange(New String() {"", "Clear", "①：テスト1", "②：テスト2", "③：テスト3", "④：テスト4", "⑤:テスト5"})

            .SelectedIndex = 0
            .DropDownStyle = ComboBoxStyle.DropDownList
        End With
    End Sub

    ' ComboBox選択変更イベント
    Private Sub ComboBox1_TextChanged(sender As Object, e As EventArgs) Handles ComboBox1.TextChanged
        selectedComboIndex = ComboBox1.SelectedIndex
        C1FlexGrid1.Select()
    End Sub

    '' フォームクローズ時のリソース解放
    'Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
    '    Try
    '        CloseQrPort()      ' ← 追加（シリアル切断）
    '        CloseConnections() ' 既存DB切断
    '    Catch ex As Exception
    '        WriteErrorLog("OnFormClosing", ex.Message)
    '    Finally
    '        MyBase.OnFormClosing(e)
    '    End Try
    'End Sub

    ' データベース接続のクローズ
    Private Sub CloseConnections()
        Try
            If cn1 IsNot Nothing AndAlso cn1.State = ConnectionState.Open Then
                cn1.Close()
            End If
            If cn2 IsNot Nothing AndAlso cn2.State = ConnectionState.Open Then
                cn2.Close()
            End If
        Catch ex As Exception
            WriteErrorLog("CloseConnections", ex.Message)
        End Try
    End Sub

    'DEMOスタート　画像読み込み
    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click

        Dim imagePath As String = System.IO.Path.Combine(Application.StartupPath, "Sample.png")
        If System.IO.File.Exists(imagePath) Then
            C1FlexGrid1.BackgroundImage = Image.FromFile(imagePath)
            C1FlexGrid1.BackgroundImageLayout = ImageLayout.Stretch
        End If

        '作業番号採番　コード読み取りによるトリガー処理？　2Dコード重複チェック
        Dim strSQL As String
        Dim cmd As NpgsqlCommand
        Dim rs As NpgsqlDataReader
        Dim Num As Integer


        strSQL = ""
        strSQL = strSQL + "SELECT COUNT(*) FROM work01 "
        strSQL = strSQL + "WHERE work2d = '" & DP2 & "';"
        cn1.Open()
        cmd = New NpgsqlCommand
        cmd.Connection = cn1
        cmd.CommandText = strSQL
        cmd.CommandType = CommandType.Text
        Num = cmd.ExecuteScalar
        cn1.Close()

        If Num = 0 Then
            '新規登録処理 採番処理
            strSQL = ""
            strSQL = strSQL + "SELECT get_prno(" & "'" & DP2 & "'" & ");"
            cn1.Open()
            cmd = New NpgsqlCommand
            cmd.Connection = cn1
            cmd.CommandText = strSQL
            cmd.CommandType = CommandType.Text
            rs = cmd.ExecuteReader
            rs.Read()
            PR1 = rs(0).ToString
            rs.Close()
            cn1.Close()
            '作業番号表示
            TextBox1.Text = PR1

        Else
            MessageBox.Show("このコードは既に登録・処理されています。", "警告", MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
            '過去の作業結果表示へ
            strSQL = ""
            strSQL = strSQL + "SELECT * FROM work01 "
            strSQL = strSQL + "WHERE work2d = '" & DP2 & "';"
            cn1.Open()
            cmd = New NpgsqlCommand
            cmd.Connection = cn1
            cmd.CommandText = strSQL
            cmd.CommandType = CommandType.Text
            rs = cmd.ExecuteReader
            rs.Read()
            PR1 = rs(0).ToString
            rs.Close()
            cn1.Close()
            '作業番号表示
            TextBox1.Text = PR1

            ' 作業データをc1flexgrid2に表示する
            DisplayWorkDataInGrid2(PR1)
            'c1flexgrid1に過去の作業結果を表示する
            ' 過去の作業結果をグリッドへ反映
            LoadPastWorkData(PR1)


        End If




    End Sub

    ' 過去の作業結果をグリッドへ反映する
    Private Sub LoadPastWorkData(workno As String)
        Try
            ClearWorkCells()

            Dim sql As String =
            "SELECT xp, yp, dno " &
            "FROM work02 " &
            "WHERE workno = @workno " &
            "ORDER BY workdate, xp, yp;"

            Using cn As New NpgsqlConnection(cn1.ConnectionString),
              cmd As New NpgsqlCommand(sql, cn)

                cmd.Parameters.AddWithValue("@workno", workno)
                cn.Open()

                Using rs As NpgsqlDataReader = cmd.ExecuteReader()
                    While rs.Read()
                        Dim xp As Integer = Convert.ToInt32(rs("xp"))
                        Dim yp As Integer = Convert.ToInt32(rs("yp"))
                        Dim dno As Integer = Convert.ToInt32(rs("dno"))

                        If yp > 0 AndAlso yp <= GRID_SIZE AndAlso xp >= 0 AndAlso xp <= GRID_SIZE Then
                            ApplyWorkStyleFromDno(yp, xp, dno)
                        End If
                    End While
                End Using
            End Using

        Catch ex As Exception
            WriteErrorLog("LoadPastWorkData", ex.Message)
            MessageBox.Show("過去の作業結果の表示中にエラーが発生しました。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ApplyWorkStyleFromDno(rowIndex As Integer, colIndex As Integer, dno As Integer)
        Select Case dno
            Case 1
                ApplyCellStyle(rowIndex, colIndex, COMBO_INDEX_STYLE1)
            Case 2
                ApplyCellStyle(rowIndex, colIndex, COMBO_INDEX_STYLE2)
            Case 3
                ApplyCellStyle(rowIndex, colIndex, COMBO_INDEX_STYLE3)
            Case 4
                ApplyCellStyle(rowIndex, colIndex, COMBO_INDEX_STYLE4)
            Case 5
                ApplyCellStyle(rowIndex, colIndex, COMBO_INDEX_STYLE5)
            Case Else
                C1FlexGrid1.SetData(rowIndex, colIndex, "")
        End Select
    End Sub

    Private Sub ClearWorkCells()
        For row As Integer = 1 To GRID_SIZE
            For col As Integer = 0 To GRID_SIZE
                C1FlexGrid1.SetData(row, col, "")
                C1FlexGrid1.SetCellStyle(row, col, C1FlexGrid1.Styles.Normal)
            Next
        Next
    End Sub




    '作業登録
    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        'temp_work02の作業データをwork02へ登録する

        InsertWorkDataToWork02(TextBox1.Text)

    End Sub

    '作業中データベース登録 temp_work02→work02へ登録する 
    Private Sub InsertWorkDataToWork02(workno As String)

        Dim sql = "INSERT INTO work02 (workno,xp,yp,dno,workdate) SELECT workno,xp,yp,dno,workdate FROM temp_work02 WHERE workno=@workno;"
        Using cn As New NpgsqlConnection(cn1.ConnectionString),
            cmd As New NpgsqlCommand(sql, cn)
            cmd.Parameters.AddWithValue("@workno", workno)
            cn.Open()
            cmd.ExecuteNonQuery()
            cn.Close()

            '登録後、temp_work02の作業データを削除する
            DeleteWorkData(workno)

            'c1flexgrid2の表示をクリアする
            C1FlexGrid2.Rows.Count = 1

            'comboboxを初期化する
            ComboBox1.SelectedIndex = COMBO_INDEX_NONE
            'textboxをクリアする
            TextBox1.Text = ""
            TextBox2.Text = ""
            TextBox3.Text = ""
            TextBox4.Text = ""

            'c1flexgrid1の表示をクリアする
            InitializeGrid()


        End Using

    End Sub




    '作業中データベース登録処理　作業番号、作業位置、作業内容を登録する　temp_work02
    Private Sub InsertWorkData(workno As String, xp As Integer, yp As Integer, dno As Integer)
        Dim sql = "INSERT INTO temp_work02 (workno,xp,yp,dno,workdate,sflg) VALUES (@workno,@xp,@yp,@dno,@workdate,@sflg);"
        Using cn As New NpgsqlConnection(cn1.ConnectionString),
            cmd As New NpgsqlCommand(sql, cn)
            cmd.Parameters.AddWithValue("@workno", workno)
            cmd.Parameters.AddWithValue("@xp", xp)
            cmd.Parameters.AddWithValue("@yp", yp)
            cmd.Parameters.AddWithValue("@dno", dno)
            cmd.Parameters.AddWithValue("@workdate", DateTime.Now)
            cmd.Parameters.AddWithValue("@sflg", "0")
            cn.Open()
            cmd.ExecuteNonQuery()
            cn.Close()
        End Using
    End Sub

    '作業中データベース登録削除　temp_work02内のレコードを削除する
    Private Sub DeleteWorkData(workno As String)
        Dim sql = "DELETE FROM temp_work02 WHERE workno=@workno;"
        Using cn As New NpgsqlConnection(cn1.ConnectionString),
            cmd As New NpgsqlCommand(sql, cn)
            cmd.Parameters.AddWithValue("@workno", workno)
            cn.Open()
            cmd.ExecuteNonQuery()
            cn.Close()
        End Using
    End Sub

    '作業中データベース登録削除　作業番号、作業位置、作業内容を削除する　temp_work02
    Private Sub DeleteWorkDataALL(workno As String, xp As Integer, yp As Integer)
        Dim sql = "DELETE FROM temp_work02 WHERE workno=@workno AND xp=@xp AND yp=@yp;"
        Using cn As New NpgsqlConnection(cn1.ConnectionString),
            cmd As New NpgsqlCommand(sql, cn)
            cmd.Parameters.AddWithValue("@workno", workno)
            cmd.Parameters.AddWithValue("@xp", xp)
            cmd.Parameters.AddWithValue("@yp", yp)
            cn.Open()
            cmd.ExecuteNonQuery()
            cn.Close()
        End Using
    End Sub



    ' シリアルポートオープン
    Private Function OpenQrPort() As Boolean
        Try
            If _port IsNot Nothing AndAlso _port.IsOpen Then
                Return True
            End If

            _port = New SerialPort("COM3", 9600, Parity.None, 8, StopBits.One) With {
                .Handshake = Handshake.None,
                .DtrEnable = True,
                .RtsEnable = False,
                .Encoding = System.Text.Encoding.ASCII,
                .NewLine = vbCrLf,
                .ReceivedBytesThreshold = 1
            }

            AddHandler _port.DataReceived, AddressOf Port_DataReceived
            _port.Open()

            Return True

        Catch ex As UnauthorizedAccessException
            WriteErrorLog("OpenQrPort", ex.Message)
            MessageBox.Show("QRリーダーのポートを開けません", "通信エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning)

        Catch ex As IO.IOException
            WriteErrorLog("OpenQrPort", ex.Message)
            MessageBox.Show("QRリーダーが接続されていないか、ポート状態が不正です", "通信エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning)

        Catch ex As ArgumentException
            WriteErrorLog("OpenQrPort", ex.Message)
            MessageBox.Show("COMポート設定が不正です", "設定エラー", MessageBoxButtons.OK, MessageBoxIcon.Error)

        Catch ex As InvalidOperationException
            WriteErrorLog("OpenQrPort", ex.Message)
            MessageBox.Show("QRリーダーのポート初期化に失敗しました", "通信エラー", MessageBoxButtons.OK, MessageBoxIcon.Error)

        Catch ex As Exception
            WriteErrorLog("OpenQrPort", ex.Message)
            MessageBox.Show("QRリーダー接続時に予期しないエラーが発生しました", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try

        If _port IsNot Nothing Then
            Try
                RemoveHandler _port.DataReceived, AddressOf Port_DataReceived
                If _port.IsOpen Then _port.Close()
                _port.Dispose()
            Catch
            End Try
            _port = Nothing
        End If

        Return False
    End Function

    Private Sub Port_DataReceived(sender As Object, e As SerialDataReceivedEventArgs)
        Try
            Dim sp = DirectCast(sender, SerialPort)
            Dim raw As String = sp.ReadExisting() ' まずは生受信を確認

            Dim parts As QrCodeParts
            If QrCodeParts.TryParse(raw, parts) Then

                Me.BeginInvoke(Sub()
                                   TextBox3.Text = parts.Hinban
                                   TextBox4.Text = "20" & parts.Kyear & "/" & parts.Kmonth & "/" & parts.Kday


                                   'DEMO用を参考に、rawを引数としてStartWork01を呼び出す
                                   StartWork01(parts)


                               End Sub)

            End If


        Catch
        End Try
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        Try
            CloseQrPort()      ' ← 追加（シリアル切断）
            CloseConnections() ' 既存DB切断
        Catch ex As Exception
            WriteErrorLog("OnFormClosing", ex.Message)
        Finally
            MyBase.OnFormClosing(e)
        End Try
    End Sub
    ' 追加：QRポート解放専用メソッド
    Private Sub CloseQrPort()
        Try
            If _port IsNot Nothing Then
                RemoveHandler _port.DataReceived, AddressOf Port_DataReceived
                If _port.IsOpen Then
                    _port.Close()
                End If
                _port.Dispose()
                _port = Nothing
            End If
        Catch ex As Exception
            WriteErrorLog("CloseQrPort", ex.Message)
        End Try
    End Sub




    Private Sub StartWork01(parts As QrCodeParts)

        'Dim imagePath As String = System.IO.Path.Combine(Application.StartupPath, "Sample.png")
        'If System.IO.File.Exists(imagePath) Then
        '    C1FlexGrid1.BackgroundImage = Image.FromFile(imagePath)
        '    C1FlexGrid1.BackgroundImageLayout = ImageLayout.Stretch
        'End If

        '作業番号採番　コード読み取りによるトリガー処理？　2Dコード重複チェック
        Dim strSQL As String
        Dim cmd As NpgsqlCommand
        Dim rs As NpgsqlDataReader
        Dim Num As Integer


        strSQL = ""
        strSQL = strSQL + "SELECT COUNT(*) FROM work01 "
        strSQL = strSQL + "WHERE work2d = '" & parts.Raw & "';"
        cn1.Open()
        cmd = New NpgsqlCommand
        cmd.Connection = cn1
        cmd.CommandText = strSQL
        cmd.CommandType = CommandType.Text
        Num = cmd.ExecuteScalar
        cn1.Close()

        If Num = 0 Then
            '新規登録処理 採番処理
            strSQL = ""
            strSQL = strSQL + "SELECT get_prno(" & "'" & parts.Raw & "'" & ");"
            cn1.Open()
            cmd = New NpgsqlCommand
            cmd.Connection = cn1
            cmd.CommandText = strSQL
            cmd.CommandType = CommandType.Text
            rs = cmd.ExecuteReader
            rs.Read()
            PR1 = rs(0).ToString
            rs.Close()
            cn1.Close()
            '作業番号表示
            TextBox1.Text = PR1

            '必要な画像を検索
            DisplayWorkDataInGrid2_S(parts.Hinban)

        Else
            MessageBox.Show("このコードは既に登録・処理されています。", "警告", MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
            '過去の作業結果表示へ
            strSQL = ""
            strSQL = strSQL + "SELECT * FROM work01 "
            strSQL = strSQL + "WHERE work2d = '" & parts.Raw & "';"
            cn1.Open()
            cmd = New NpgsqlCommand
            cmd.Connection = cn1
            cmd.CommandText = strSQL
            cmd.CommandType = CommandType.Text
            rs = cmd.ExecuteReader
            rs.Read()
            PR1 = rs(0).ToString
            rs.Close()
            cn1.Close()
            '作業番号表示
            TextBox1.Text = PR1

            ' 作業データをc1flexgrid2に表示する
            DisplayWorkDataInGrid2_S(parts.Hinban)
            'DisplayWorkDataInGrid2(PR1)

            'c1flexgrid1に過去の作業結果を表示する
            LoadPastWorkData(PR1)
            'c2flexgrid1に過去の作業結果を表示する
            DisplayWorkDataInGrid2K(PR1)





        End If




    End Sub








End Class


































































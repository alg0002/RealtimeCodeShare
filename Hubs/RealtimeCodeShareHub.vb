Imports System.Collections.Concurrent
Imports Microsoft.AspNet.SignalR
Imports Microsoft.AspNet.SignalR.Hubs

<HubName("realtimeCodeShare")>
Public Class RealtimeCodeShareHub
	Inherits Hub

	Private Const IndexPageName As String = "index"

	'誰も使わなくなったログ等を破棄するまでの時間
	Private DisposeTime As TimeSpan = TimeSpan.FromDays(7)	' 7日間

	'roomごとのチャットログ
	Private Shared ReadOnly logs As New ConcurrentDictionary(Of String, List(Of String()))

	'roomごとのコードおよびposition
	Private Shared ReadOnly codes As New ConcurrentDictionary(Of String, Object())

	'roomごとのlanguage
	Private Shared ReadOnly modes As New ConcurrentDictionary(Of String, String)

	'roomごとのConnectionIDおよび最終切断日時
	Private Shared ReadOnly connections As New ConcurrentDictionary(Of String, Dictionary(Of String, DateTime))

	Public Overrides Function OnConnected() As Threading.Tasks.Task
		Dim t As Threading.Tasks.Task = MyBase.OnConnected()
		Dim currentDateTime As DateTime = Now()
		Dim roomnames(connections.Keys.Count - 1) As String
		connections.Keys.CopyTo(roomnames, 0)
		For Each roomname As String In roomnames
			Dim connectionids(connections(roomname).Keys.Count - 1) As String
			connections(roomname).Keys.CopyTo(connectionids, 0)
			For Each connectionid As String In connectionids
				If currentDateTime - connections(roomname)(connectionid) > DisposeTime Then
					'対象接続IDを破棄
					connections(roomname).Remove(connectionid)
					'誰も接続していない状態になったらlogsとcodesも破棄
					If connections(roomname).Count = 0 Then
						connections.TryRemove(roomname, Nothing)
						If logs.ContainsKey(roomname) Then
							logs.TryRemove(roomname, Nothing)
						End If
						If codes.ContainsKey(roomname) Then
							codes.TryRemove(roomname, Nothing)
						End If
						If modes.ContainsKey(roomname) Then
							modes.TryRemove(roomname, Nothing)
						End If
					End If
				End If
			Next
		Next
		Return t
	End Function

	Public Overrides Function OnDisconnected() As Threading.Tasks.Task
		Dim t As Threading.Tasks.Task = MyBase.OnDisconnected()
		For Each key As String In connections.Keys
			If connections(key).ContainsKey(Context.ConnectionId) Then
				connections(key)(Context.ConnectionId) = Now()
			End If
		Next
		Return t
	End Function

	Public Overrides Function OnReconnected() As Threading.Tasks.Task
		Dim t As Threading.Tasks.Task = MyBase.OnReconnected()
		For Each key As String In connections.Keys
			If connections(key).ContainsKey(Context.ConnectionId) Then
				connections(key)(Context.ConnectionId) = DateTime.MaxValue
			End If
		Next
		Return t
	End Function

	Public Sub joinIndex()
		Groups.Add(Context.ConnectionId, IndexPageName)
		If connections.ContainsKey(IndexPageName) Then
			connections(IndexPageName).Add(Context.ConnectionId, DateTime.MaxValue)
		Else
			Dim l As New Dictionary(Of String, DateTime)
			l.Add(Context.ConnectionId, DateTime.MaxValue)
			connections.TryAdd(IndexPageName, l)
		End If
	End Sub

	Public Sub getRoomList()
		If logs.Count > 0 Then
			Clients.Group(IndexPageName).setRoomList(logs.Keys)
		End If
	End Sub

	Public Sub joinRoom(roomname As String)
		Groups.Add(Context.ConnectionId, roomname)
		If logs.ContainsKey(roomname) Then
			Clients.Caller.setPastMsgs(logs(roomname))
		Else
			logs.TryAdd(roomname, New List(Of String()))
		End If
		If codes.ContainsKey(roomname) AndAlso codes(roomname) IsNot Nothing AndAlso codes(roomname).Length > 1 Then
			Clients.Caller.setCode(codes(roomname)(0), codes(roomname)(1))
		Else
			codes.TryAdd(roomname, Nothing)
		End If
		If modes.ContainsKey(roomname) Then
			Clients.Caller.setMode(modes(roomname))
		Else
			modes.TryAdd(roomname, "javascript")
		End If
		If connections.ContainsKey(roomname) Then
			connections(roomname).Add(Context.ConnectionId, DateTime.MaxValue)
		Else
			Dim l As New Dictionary(Of String, DateTime)
			l.Add(Context.ConnectionId, DateTime.MaxValue)
			connections.TryAdd(roomname, l)
		End If
		getRoomList()
	End Sub

	Public Sub shareCode(roomname As String, code As String, position As Object)
		Clients.Group(roomname, Context.ConnectionId).setCode(code, position)
		Dim item() As Object = {code, position}
		codes(roomname) = item
	End Sub

	Public Sub shareMode(roomname As String, langmode As String)
		Clients.Group(roomname).setMode(langmode)
		modes(roomname) = langmode
	End Sub

	Public Sub chat(roomname As String, msg As String, name As String)
		'各クライアントにメッセージを送信
		Clients.Group(roomname).setChatMsg(Now().ToString("HH:mm:ss"), name, msg)

		'メッセージをログに残す
		Dim log As List(Of String())
		If logs.ContainsKey(roomname) Then
			log = logs(roomname)
		Else
			log = New List(Of String())
		End If
		log.Add(New String() {Now().ToString("HH:mm:ss"), name, msg})
		logs.TryAdd(roomname, log)
		If logs(roomname).Count > 50 Then
			logs(roomname).RemoveAt(0)
		End If
	End Sub

End Class

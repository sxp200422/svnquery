<%@ Page Language="C#" AutoEventWireup="true" Inherits="SvnWebQuery.View" Codebehind="View.aspx.cs" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta http-equiv="Content-Type" content="text/html; charset=UTF-8" />
	<title></title>	
    <link href="styles/shCore.css" rel="stylesheet" type="text/css" />
    <link href="styles/shThemeDefault.css" rel="stylesheet" type="text/css" />
	<script type="text/javascript" src="scripts/shCore.js"></script>
</head>
<body style="font-family: arial; font-size: 10pt; background-color: #F0F3FF;">

    <h2 id="_header" runat="server">
        Path/to/file</h2>
    <table id="_properties" runat="server">
        <tr>
            <td>Author:</td><td id="_author" runat="server" ></td>
        </tr>
        <tr>
            <td>Modified:</td><td id="_modified" runat="server" ></td>
        </tr>
        <tr>
            <td>Revisions:</td><td id="_revisions" runat="server" ></td>
        </tr>
        <tr id="_sizeRow" runat="server">
            <td>File Size:</td><td id="_size" runat="server" ></td>
        </tr>
        <tr>
            <td valign="top">Message:</td><td id="_message" runat="server" style="white-space: pre"></td>
        </tr>
    </table>
    <p id="_contentWarning" runat="server" style="font-style:italic; color:Gray"></p>
    
    <pre id="_content" runat="server" style="font-family:Consolas,Courier New;"></pre>
    
</body>
</html>

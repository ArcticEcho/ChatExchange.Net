$root = (split-path -parent $MyInvocation.MyCommand.Definition) + '\..'
$version = [System.Reflection.Assembly]::LoadFile("$root\ChatExchange.Net\bin\Release\ChatExchange.Net.dll").GetName().Version
$versionStr = "{0}.{1}.{2}-beta" -f ($version.Major, $version.Minor, $version.Revision)

Write-Host "Setting .nuspec version tag to $versionStr"

$content = (Get-Content $root\NuGet\ChatExchange.Net.nuspec) 
$content = $content -replace '\$version\$',$versionStr

$content | Out-File $root\nuget\ChatExchange.Net.compiled.nuspec

& $root\NuGet\NuGet.exe pack $root\nuget\ChatExchange.Net.compiled.nuspec
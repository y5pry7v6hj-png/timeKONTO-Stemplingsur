dotnet publish -f net10.0-maccatalyst \
-c Release \
-r maccatalyst-arm64 \
--self-contained true \
-o ./publish-mac





dotnet publish -f net10.0-android -c Release \
-r android-arm \
-p:AndroidPackageFormats=apk \
-o ./publish-andoid-arm

dotnet publish -f net10.0-android -c Release \
-r android-arm64 \
-p:AndroidPackageFormats=apk \
-o ./publish-andoid-arm64
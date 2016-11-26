./TestEchoPrintSharp/bin/Debug/TestEchoPrintSharp.exe > code
c=`cat code`
curl https://echoprint.c3s.cc/query?fp_code=$c
# cat code | tr '_' '/' | tr '-' '+' | base64 -d | zlib-flate -uncompress

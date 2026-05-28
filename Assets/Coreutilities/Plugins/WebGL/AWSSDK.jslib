mergeInto(LibraryManager.library, {
  UploadToS3_JS: function(dataPtr, length, jsonCredentialsPtr) {
    console.log("UploadToS3_JS start");
    const bytes = new Uint8Array(Module.HEAPU8.buffer, dataPtr, length);
    var configStr = UTF8ToString(jsonCredentialsPtr);
    const config = JSON.parse(configStr)

    console.log("UploadToS3_JS doUpload");
    console.log("config", config);
    AWS.config.update({
      region: config.region,
      credentials: new AWS.Credentials(
        config.accessKeyId,
        config.secretAccessKey,
        config.sessionToken
      )
    });

    const s3 = new AWS.S3();
    var params = {
      Bucket: config.bucket,
      Key: config.key,
      Body: bytes,
      ContentType: "image/png"
    };
    console.log("params", params);
    console.log("typeof Bucket", config.bucket); // 應該是 string
    console.log("typeof Key", config.key);       // 應該是 string
    console.log("typeof Body", typeof bytes);       // 應該是 object (Uint8Array)
    s3.putObject(params, function(err, data) {
      if (err) {
          SendMessage("AWSUtils", "OnUploadError", err.message);
      } else {
          SendMessage("AWSUtils", "OnUploadSuccess", config.key);
      }
    });
  }
});
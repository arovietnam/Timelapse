var Index = function () {
    var EvercamApi = "https://api.evercam.io/v1";
    var timelapseApiUrl = "http://timelapse.evercam.io/v1/timelapses";
    var utilsApi = "http://timelapse.evercam.io/v1";
    var ApiAction = 'POST';
    var apiContentType = 'application/json; charset=utf-8';
    var loopCount = 1;
    var user = null;

    $("#btnAnotherUser").live("click", function () {
        $("#user_email").val("");
        $("#divEmailInput").show();
        $("#divRemember").show();
        $("#divEmail").hide();
        $("#lblEmail").text("");
        $("#user_email").focus();
        $("#btnAnotherUser").hide();
        $("#user_password").val("");
        $("#user_remember_me").attr("checked", false);
    });

    var getParameterByName = function(name, searchString) {
        name = name.replace(/[\[]/, "\\\[").replace(/[\]]/, "\\\]");
        var regex = new RegExp("[\\?&]" + name + "=([^&#]*)"),
            results = regex.exec(searchString); //location.search);
        return results == null ? null : decodeURIComponent(results[1].replace(/\+/g, " "));
    };

    var getQueryStringByName = function(name) {
        name = name.replace(/[\[]/, "\\\[").replace(/[\]]/, "\\\]");
        var regex = new RegExp("[\\?&]" + name + "=([^&#]*)"),
            results = regex.exec(location.search);
        return results == null ? null : decodeURIComponent(results[1].replace(/\+/g, " "));
    };

    function toTitleCase(str) {
        //return str.replace(/\w\S*/g, function (txt) { return txt.charAt(0).toUpperCase() + txt.substr(1).toLowerCase(); });
        return str.charAt(0).toUpperCase() + str.substr(1);
    }

    function autoLogIn(code) {
        var form = document.createElement("form");
        var element1 = document.createElement("input");
        var element2 = document.createElement("input");
        var element3 = document.createElement("input");
        var element4 = document.createElement("input");
        var element5 = document.createElement("input");

        form.method = "POST";
        form.action = "https://dashboard.evercam.io/oauth2/token";

        element1.value = code;
        element1.name = "code";
        form.appendChild(element1);

        element2.value = c4203c3e;
        element2.name = "client_id";
        form.appendChild(element2);

        element2.value = "55e2df6518ec146e0d968d640064017d";
        element2.name = "client_secret";
        form.appendChild(element2);

        element2.value = "http://astimegoes.by";
        element2.name = "redirect_uri";
        form.appendChild(element2);

        element2.value = "authorization_code";
        element2.name = "grant_type";
        form.appendChild(element2);

        document.body.appendChild(form);

        form.submit();
    }

    var format = function(state) {
        if (!state.id) return state.text; // optgroup
        return "<img class='flag' src='assets/img/flags/" + state.id.toLowerCase() + ".png'/>&nbsp;&nbsp;" + state.text;
    };
    
    var handleLoginSection = function () {
        
        if (localStorage.getItem("api_id") != null && localStorage.getItem("api_id") != undefined &&
            localStorage.getItem("api_key") != null && localStorage.getItem("api_key") != undefined) {
            user = JSON.parse(localStorage.getItem("user"));
            getMyTimelapse();
        }
        else {
            window.location = 'login.html';
            $("#divTimelapses").html('');
            $(".fullwidthbanner-container").show();
            
            $(".default-timelapse").show();
            $("#liUsername").hide();
            $("#lnkSignout").hide();
            $(".timelapseContainer").hide();
            $(".responsivenav").removeClass("btn");
            $(".responsivenav").removeClass("btn-navbar");
        }
    }

    var clearPage = function() {
        $(".default-timelapse").html("");
        $(".default-timelapse").hide();
        $("#liUsername").show();
        $("#lnkSignout").show();
        $("#btnNewTimelapse").show();
        $("#divMainContainer").removeClass("container-bg");

        $("#newTimelapse").html("");
        $("#newTimelapse").fadeOut();
    };

    var handleLogout = function() {
        $("#lnkLogout").bind("click", function() {
            localStorage.removeItem("api_id");
            localStorage.removeItem("api_key");
            localStorage.removeItem("user");
            localStorage.removeItem("timelapseCameras");
            localStorage.removeItem("sharedcameras");
            window.location = 'login.html';
        });
    };

    var handleNewTimelapse = function() {
        $("#lnNewTimelapse").on("click", function () {
            showTimelapseForm();
        });

        $("#lnNewTimelapseCol").bind("click", function() {
            $("#newTimelapse").slideUp(500, function() {
                $("#newTimelapse").html("");
                $("#lnNewTimelapse").show();
                $("#lnNewTimelapseCol").hide();
            });

            ApiAction = 'POST';
            $("#txtCameraCode0").val('');
        });

        $("#lnNewCamera").bind("click", function() {
            showNewCameraForm();
        });
        $("#lnNewCameraCol").bind("click", function() {
            $("#newTimelapse").slideUp(500, function() {
                $("#newTimelapse").html("");
                $("#lnNewCamera").show();
                $("#lnNewCameraCol").hide();
            });
        });
    };

    var showNewCameraForm = function() {
        $.get('NewCameraForm.html', function(data) {
            $("#newTimelapse").html(data);
            $("#lnNewCamera").hide();
            $("#lnNewCameraCol").show();

            $("#lnNewTimelapse").show();
            $("#lnNewTimelapseCol").hide();
            $("#newTimelapse").slideDown(500);
        });
    };

    $(".newTimelapse").live("click", function () {
        showTimelapseForm();
        $("#divLoadingTimelapse").fadeOut();
    });

    var showTimelapseForm = function () {
        $.get('NewTimelapse.html?' + Math.random(), function (data) {
            $("#newTimelapse").html(data);
            $("#lnNewTimelapse").hide();
            $("#lnNewTimelapseCol").show();
            handleFileupload();
            getCameras(true);
            $('.timerange').timepicker({
                minuteStep: 1,
                showSeconds: false,
                showMeridian: false,
                defaultTime: false
            });
            var dates = $(".daterange").datepicker({
                format: 'dd/mm/yyyy',
                minDate: new Date()
            });
            $("#newTimelapse").slideDown(500);
        });
    };

    $('[name="TimeRange"]').live("click", function () {
        var id = $(this).attr("id");
        var dataval = $(this).attr("data-val");
        if (id == "chkTimeRange" + dataval) {
            $("#divTimeRange" + dataval).slideDown();
        }
        else
            $("#divTimeRange" + dataval).slideUp();
    });

    $('[name="DateRange"]').live("click", function () {
        var id = $(this).attr("id");
        var dataval = $(this).attr("data-val");
        if (id == "chkDateRange" + dataval) {
            $("#divDateRange" + dataval).slideDown();
        }
        else
            $("#divDateRange" + dataval).slideUp();
    });

    var handleMyTimelapse = function() {
        $("#lnMyTimelapse").bind("click", function() {
            getMyTimelapse();
        });
    };

    $(".formButtonCancel").live("click", function () {
        var id = $(this).attr("data-val");
        if (id != "0") {
            var code = $("#txtCameraCode" + id).val();
        }
        $("#newTimelapse").slideUp(500, function () {
            $("#newTimelapse").html("");
            $("#lnNewTimelapse").show();
            $("#lnNewTimelapseCol").hide();
        });

        ApiAction = 'POST';
        $("#txtCameraCode0").val('');
        if ($("#divTimelapses").html() == "")
            $("#divLoadingTimelapse").fadeIn();
    });

    $(".formButtonOk").live("click", function () {
        var timelapseId = $(this).attr("data-val");
        $("#divAlert" + timelapseId).removeClass("alert-info").addClass("alert-error");
        if ($("#ddlCameras" + timelapseId).val() == "") {
            $("#divAlert" + timelapseId).slideDown();
            $("#divAlert" + timelapseId + " span").html("Please select camera to continue.");
            return;
        }
        if ($("#txtTitle" + timelapseId).val() == '') {
            $("#divAlert" + timelapseId).slideDown();
            $("#divAlert" + timelapseId + " span").html("Please enter timelapse title.");
            return;
        }
        if ($("#ddlIntervals" + timelapseId).val() == 0) {
            $("#divAlert" + timelapseId).slideDown();
            $("#divAlert" + timelapseId + " span").html("Please select timelapse interval.");
            return;
        }
        var d = new Date();
        var fromDate = d.getDate() + '/' + (d.getMonth()+1) + '/' + d.getFullYear();
        var toDate = fromDate;
        var fromTime = "00:00";
        var toTime = fromTime;
        var dateAlways = true;
        var timeAlways = true;
        if ($("#chkDateRange" + timelapseId).is(":checked")) {
            dateAlways = false;
            fromDate = $("#txtFromDateRange" + timelapseId).val();
            if (fromDate == "")
            {
                $("#divAlert" + timelapseId).slideDown();
                $("#divAlert" + timelapseId + " span").html("Please select from date range.");
                return;
            }
            toDate = $("#txtToDateRange" + timelapseId).val();
            if (toDate == "") {
                $("#divAlert" + timelapseId).slideDown();
                $("#divAlert" + timelapseId + " span").html("Please select to date range.");
                return;
            }
            if (!validateDates(fromDate, toDate, timelapseId)) {
                return;
            }
        }
        if ($("#chkTimeRange" + timelapseId).is(":checked")) {
            timeAlways = false;
            fromTime = $("#txtFromTimeRange" + timelapseId).val();
            if (fromTime == "") {
                $("#divAlert" + timelapseId).slideDown();
                $("#divAlert" + timelapseId + " span").html("Please select from time range.");
                return;
            }
            toTime = $("#txtToTimeRange" + timelapseId).val();
            if (toTime == "") {
                $("#divAlert" + timelapseId).slideDown();
                $("#divAlert" + timelapseId + " span").html("Please select to time range.");
                return;
            }
            if (fromTime == toTime) {
                $("#divAlert" + timelapseId).slideDown();
                $("#divAlert" + timelapseId + " span").html('To time and from time cannot be same.');
                return;
            }
        }
        var timezone = "GMT Standard Time";
        var cams = JSON.parse(localStorage.getItem("timelapseCameras"));
        for (var i = 0; i < cams.cameras.length; i++) {
            if (cams.cameras[i].id == $("#ddlCameras" + timelapseId).val())
                timezone = cams.cameras[i].timezone;
        }
        cams = JSON.parse(localStorage.getItem("sharedcameras"));
        if (cams != null && cams != undefined) {
            for (var i = 0; i < cams.cameras.length; i++) {
                if (cams.cameras[i].id == $("#ddlCameras" + timelapseId).val())
                    timezone = cams.cameras[i].timezone;
            }
        }
        
        var camCode = "/users/" + user.id;
        if ($("#txtTimelapseId").val() != "") {
            ApiAction = 'PUT';
            apiContentType = "application/x-www-form-urlencoded";
            camCode = "/" + $("#txtCameraCode" + timelapseId).val() + "/users/" + user.id;
        }
        var o = {
            "camera_eid": $("#ddlCameras" + timelapseId).val(),
            "access_token": "",
            "from_time": fromTime,
            "to_time": toTime,
            "from_date": fromDate,
            "to_date": toDate,
            "title": $("#txtTitle" + timelapseId).val(),
            "time_zone": timezone,
            "enable_md": false,
            "md_thrushold": 0,
            "exclude_dark": false,
            "darkness_thrushold": 0,
            "privacy": 0,
            "is_recording": $("#chkRecordingTimelapse" + timelapseId).is(":checked"),
            "is_date_always": dateAlways,
            "is_time_always": timeAlways,
            "interval": $("#ddlIntervals" + timelapseId).val(),
            "fps": $("#ddlFrameRate" + timelapseId).val()
        };
        
        if ($('.table-striped td.name').html() != undefined) {
            $(".fileupload-buttonbar").hide();
            $("#divAlert" + timelapseId).removeClass("alert-error").addClass("alert-info");
            $("#divAlert" + timelapseId).slideDown();
            $("#divAlert" + timelapseId + " span").html('<img src="assets/img/loader3.gif"/>&nbsp;Saving Twitter Response');
            $('.table-striped td.start button.btn').click();
            setTimeout(function () { checkFileUploadAndSave(timelapseId, camCode, o, $(".progress-success").attr("aria-valuemin")); }, 1000);
        }
        else
            checkFileUploadAndSave(timelapseId, camCode, o, null);
    });

    var fileuploadFinish = function(timelapseId, camCode, o, percentage) {
        checkFileUploadAndSave(timelapseId, camCode, o, percentage);
    };

    var checkFileUploadAndSave = function(timelapseId, camCode, o, percentage) {
        $("#divAlert" + timelapseId).removeClass("alert-error").addClass("alert-info");
        $("#divAlert" + timelapseId).slideDown();
        $("#divAlert" + timelapseId + " span").html('<img src="assets/img/loader3.gif"/>&nbsp;Saving timelapse');

        if (percentage != null && parseInt(percentage) <= 100) {
            setTimeout(function() { fileuploadFinish(timelapseId, camCode, o, $(".progress-success").attr("aria-valuemin")); }, 1000);
            return;
        } else {
            if ($('.table-striped td.preview a').length > 0)
                $("#txtLogoFile").val($('.table-striped td.preview a').attr("href"));
            else if ($("#txtLogoFile").val() == '')
                $("#txtLogoFile").val('-');
        }
        o.watermark_position = $("#ddlWatermarkPos" + timelapseId).val();
        o.watermark_file = $("#txtLogoFile").val();

        $.ajax({
            type: ApiAction,
            url: timelapseApiUrl + camCode,
            data: o,
            dataType: 'json',
            ContentType: apiContentType,//'application/json; charset=utf-8',
            success: function(data) {
                $("#divAlert" + timelapseId + " span").html('Timelapse saved.');
                if ($("#txtCameraCode0").val() != "")
                    $("#tab" + $("#txtCameraCode0").val()).remove();
                $("#divTimelapses").prepend(getHtml(data));
                getVideoPlayer(data.camera_id, data.code, data.mp4_url, data.jpg_url, data.id, data.watermark_file);
                $("#newTimelapse").slideUp(500, function() { $("#newTimelapse").html(""); });
                $("#lnNewTimelapse").show();
                $("#lnNewTimelapseCol").hide();
                if ($(".timelapseContainer").css("display") == "none")
                    $(".timelapseContainer").fadeIn();
                $("#divContainer" + data.id).slideDown(500);

                ApiAction = 'POST';
                apiContentType = 'application/json; charset=utf-8';
                setTimeout(function() {
                    $("#divAlert" + timelapseId).slideUp();
                }, 6000);
            },
            error: function(xhr, textStatus) {
                $("#divAlert" + timelapseId).removeClass("alert-info").addClass("alert-error");
                $("#divAlert" + timelapseId + " span").html('Timelapse could not be saved.');
            }
        });
    };

    $(".cameraformButtonOk").live("click", function () {
        var caneraSnaps;
        $("#divAlert0").removeClass("alert-info").addClass("alert-error");
        if ($("#txtCameraUniqueId").val() == "") {
            $("#divAlert0").slideDown();
            $("#divAlert0 span").html("Please enter unique camera ID.");
            return;
        }
        if ($("#txtCameraName").val() == '') {
            $("#divAlert0").slideDown();
            $("#divAlert0 span").html("Please enter camera name.");
            return;
        }
        if ($("#txtCameraHost").val() == "") {
            $("#divAlert0").slideDown();
            $("#divAlert0 span").html("Please enter camera host.");
            return;
        }
        if ($("#txtCameraUsername").val() != "" && $("#txtCameraPassword").val() == "") {
            $("#divAlert0").slideDown();
            $("#divAlert0 span").html("Please enter camera Password.");
            return;
        }
        if ($("#txtCameraUsername").val() == "" && $("#txtCameraPassword").val() != "") {
            $("#divAlert0").slideDown();
            $("#divAlert0 span").html("Please enter camera Username.");
            return;
        }
        
        var o = {
            "id": $("#txtCameraUniqueId").val(),
            "name": $("#txtCameraName").val(),
            "is_public": $("#rdPublicCamera").is(":checked"),
            "external_host": $("#txtCameraHost").val()
        };

        if ($("#txtCameraHttpPort").val() != "")
            o.external_http_port = $("#txtCameraHttpPort").val();
        if ($("#txtCameraRtspPort").val() != "")
            o.external_rtsp_port = $("#txtCameraRtspPort").val();

        if ($("#txtCameraUrl").val() != "") 
            o.jpg_url = $("#txtCameraUrl").val();
        if ($("#txtCameraUsername").val() != "" && $("#txtCameraPassword").val() != "") {
            o.cam_username = $("#txtCameraUsername").val();
            o.cam_password = $("#txtCameraPassword").val();
        }

        $("#divAlert0").removeClass("alert-error").addClass("alert-info");
        $("#divAlert0").slideDown();
        $("#divAlert0 span").html('<img src="assets/img/loader3.gif"/>&nbsp;Saving Camera');
        $.ajax({
            type: "POST",
            url: EvercamApi + "/cameras.json",
            crossDomain: true,
            xhrFields: {
                withCredentials: true
            },
            data: o,
            dataType: 'json',
            ContentType: "application/x-www-form-urlencoded",
            success: function (data) {
                $("#divAlert0 span").html('Camera saved.');
                $("#txtCameraUniqueId").val('');
                $("#txtCameraName").val('');
                $("#txtCameraEndpoints").val('');
                $("#txtCameraUrl").val('');
                $("#txtCameraUsername").val('');
                $("#txtCameraPassword").val('');
                setTimeout(function () {
                    $("#newTimelapse").slideUp(500, function () { $("#newTimelapse").html(""); });
                    $("#divAlert0").slideUp();
                }, 6000);
            },
            error: function (xhr, textStatus) {
                $("#divAlert0").removeClass("alert-info").addClass("alert-error");
                $("#divAlert0 span").html(xhr.responseJSON.message);
            }
        });
    });

    $(".cameraformButtonCancel").live("click", function () {
        $("#newTimelapse").slideUp(500, function () {
            $("#newTimelapse").html("");
            $("#lnNewCamera").show();
            $("#lnNewCameraCol").hide();
        });
    });

    var validateDates = function(fromDate, toDate, timelapseId) {
        var movieToStr = toDate.split("/");
        var movieFromStr = fromDate.split("/");
        var td = new Date(movieToStr[2], (movieToStr[1] - 1), movieToStr[0]);
        var fd = new Date(movieFromStr[2], (movieFromStr[1] - 1), movieFromStr[0]);
        var currentTime = new Date();
        currentTime.setHours(0);
        currentTime.setMinutes(0);
        currentTime.setSeconds(0);
        currentTime.setMilliseconds(0);

        if ($("#txtTimelapseId").val() == "" && fd < currentTime) {
            $("#divAlert" + timelapseId).slideDown();
            $("#divAlert" + timelapseId + " span").html("From date cannot be less than current time.");
            return false;
        }
        if (td < currentTime) {
            $("#divAlert" + timelapseId).slideDown();
            $("#divAlert" + timelapseId + " span").html("To date cannot be less than current time.");
            return false;
        }
        if (td < fd) {
            $("#divAlert" + timelapseId).slideDown();
            $("#divAlert" + timelapseId + " span").html('To date cannot be less than from date.');
            return false;
        }
        /*if (Date.parse(todate) == Date.parse(fromdate)) {
            $("#lblDateMsg").html("To date and from date cannot be same.");
            $("#lblDateMsg").show();
            return false;
        }
        var diff = (todate - fromdate) / 3600000;
        if (diff > 2) {
            $("#lblDateMsg").html("Cannot create clip longer than two hours.");
            $("#lblDateMsg").show();
            return false;
        }*/
        return true;
    };

    var getMyTimelapse = function() {
        $(".default-timelapse").html("");
        $(".default-timelapse").hide();
        $("#liUsername").show();
        $("#lnkSignout").show();
        $("#btnNewTimelapse").show();
        $("#divMainContainer").removeClass("container-bg");

        $("#newTimelapse").html("");
        $("#newTimelapse").fadeOut();

        $("#displayUsername").html(user.firstname + " " + user.lastname);
        $("#divLoadingTimelapse").fadeIn();
        $("#divLoadingTimelapse").html('<img src="assets/img/loader3.gif" alt="Loading..."/>&nbsp;Fetching Timelapses');
        $.ajax({
            type: 'GET',
            url: timelapseApiUrl + "/users/" + user.username,
            dataType: 'json',
            contentType: 'application/x-www-form-urlencoded; charset=UTF-8',
            success: function(data) {
                if (data.length == 0) {
                    $("#divTimelapses").html('');
                    $("#divLoadingTimelapse").html('You have not created any timelapses. <a href="javascript:;" class="newTimelapse">Click</a> to create one.');
                } else {
                    var count = 1;
                    var html = '';
                    for (var i = 0; i < data.length; i++) {
                        var timelapse = data[i];
                        $("#divTimelapses").append(getHtml(timelapse));
                        getVideoPlayer(timelapse.camera_id, timelapse.code, timelapse.mp4_url, timelapse.jpg_url, timelapse.id, timelapse.watermark_file);
                    }
                    $(".timelapseContainer").fadeIn();
                    $("#divLoadingTimelapse").fadeOut();
                    $('.timerange').timepicker({
                        minuteStep: 1,
                        showSeconds: false,
                        showMeridian: false,
                        defaultTime: false
                    });
                    handleFileupload();
                    var dates = $(".daterange").datepicker({
                        format: 'dd/mm/yyyy',
                        minDate: new Date()
                    });
                    $("pre").snippet("html", { style: "whitengrey", clipboard: "assets/scripts/ZeroClipboard.swf", showNum: false });
                }
            },
            error: function(xhr, textStatus) {
                $("#divTimelapses").html('');
                $("#divLoadingTimelapse").html('You have not created any timelapses. <a href="#">Click</a> to create one.');
            }
        });
    };

    var getTimeLapseStatus = function(status) {
        if (status == 0)
            return '<span class="green">Active</span>';
        else if (status == 1)
            return '<span class="green">Active</span>';
        else if (status == 2)
            return '<span class="red">Failed</span>';
        else if (status == 3)
            return '<span class="green">Active</span>';
        else if (status == 4)
            return '<span class="red">Stopped</span>';
        else if (status == 5)
            return '<span class="red">Camera Not Found</span>';
        else if (status == 6)
            return '<span class="green">Completed</span>';
        else if (status == 7)
            return '<span class="green">Paused</span>';
    };

    var getVideoPlayer = function (cameraId, timelapseCode, mp4, jpg, timelapseId, logoFile) {
        var img = new Image();
        img.onerror = function (evt) {
            var html = "<video data-setup='{ \"playbackRates\": [0.06, 0.12, 0.25, 0.5, 1, 1.5, 2, 2.5, 3] }' poster=\"assets/img/timelapse.jpg\" preload=\"none\" controls class=\"video-js vjs-default-skin video-bg-width\" id=\"video-control-" + timelapseId + "\">";
            html += '<source type="application/x-mpegURL" src="http://timelapse.evercam.io/timelapses/' + cameraId + '/' + timelapseId + '/index.m3u8"></source>';
            html += '</video>';
            $("#divVideoContainer" + timelapseId).html(html);
            try {
                videojs("video-control-" + timelapseId).ready(function () {
                    //this.playbackRate(0.25);
                });
            }
            catch (exception) { }
        };
        img.onload = function (evt) {
            var html = "<video data-setup='{ \"playbackRates\": [0.06, 0.12, 0.25, 0.5, 1, 1.5, 2, 2.5, 3] }' poster=\"" + jpg + "\" preload=\"none\" controls class=\"video-js vjs-default-skin video-bg-width\" id=\"video-control-" + timelapseId + "\">";
            html += '<source type="application/x-mpegURL" src="http://timelapse.evercam.io/timelapses/' + cameraId + '/' + timelapseId + '/index.m3u8"></source>';
            html += '</video>';
            $("#divVideoContainer" + timelapseId).html(html);
            try {
                videojs("video-control-" + timelapseId).ready(function () {
                    //this.playbackRate(0.25);
                });
            }
            catch (exception) { }
        };
        img.src = jpg;
    };

    var getDate = function() {
        var d = new Date();
        return FormatNumTo2(d.getDate()) + ' ' + getFullMonth(d.getMonth()) + ' ' + d.getFullYear() + ' ' + FormatNumTo2(d.getHours()) + ':' + FormatNumTo2(d.getMinutes());
    };

    var getHtml = function(data) {
        var cameraOptions = '';
        var timezone = '';
        var cameraOnline = false;
        var cameraName = '';
        var cams = JSON.parse(localStorage.getItem("timelapseCameras"));
        if (cams != null) {
            for (var i = 0; i < cams.cameras.length; i++) {
                var selected = '';
                if (cams.cameras[i].id == data.camera_id) {
                    timezone = cams.cameras[i].timezone;
                    cameraName = cams.cameras[i].name;
                    selected = 'selected';
                    cameraOnline = cams.cameras[i].is_online;
                }
                cameraOptions += '<option value="' + cams.cameras[i].id + '" ' + selected + '>' + cams.cameras[i].name + '</option>';
            }
        }
        cams = JSON.parse(localStorage.getItem("sharedcameras"));
        if (cams != null && cams != undefined) {
            for (var i = 0; i < cams.cameras.length; i++) {
                var selected = '';
                if (cams.cameras[i].id == data.camera_id) {
                    timezone = cams.cameras[i].timezone;
                    selected = 'selected';
                    cameraOnline = cams.cameras[i].is_online;
                }
                cameraOptions += '<option value="' + cams.cameras[i].id + '" ' + selected + '>' + cams.cameras[i].name + '</option>';
            }
        }
        
        var html = '    <div id="tab' + data.code + '">'; 
        html += '        <div class="header-bg">';
        html += '          <div class="row-fluid box-header-padding" data-val="' + data.id + '">';
        html += '              <div id="timelapseTitle' + data.id + '" class="timelapse-labelhd timelapse-label"><div class="' + (cameraOnline ? 'camera-online' : 'camera-offline') + '"></div>' + data.title + '&nbsp;<span class="timelapse-camera-name">' + cameraName + '</span></div>';
        html += '              <div id="timelapseStatus' + data.code + '" class="timelapse-recordhd timelapse-label-status text-right">';
        html += getTimeLapseStatus(data.status);
        html += '               </div>';
        html += '          </div>';
        html += '          <div id="divContainer' + data.id + '" class="row-fluid box-content-padding hide">';
        html += '              <div id="divVideoContainer' + data.id + '" class="span6">';
        //call videojs
        html += '                  <video data-setup="{}" preload="none" controls="" class="video-js vjs-default-skin video-bg-width" id="vde4b3u05e9y">';
        html += '                   <source type="video/mp4" src="' + data.mp4_url + '"></source>';
        html += '                  </video>';

        html += '              </div>';
        html += '              <div class="span6">';
        html += '                  <table class="tbl-tab" cellpadding="0" cellspacing="0">';
        html += '                      <thead>';
        html += '                          <tr>';
        html += '                              <th class="tbl-hd2"><a class="tab-a block' + data.id + ' selected-tab" href="javascript:;" data-ref="#stats' + data.id + '" data-val="' + data.id + '">Stats</a></th>';
        html += '                              <th class="tbl-hd1"><a class="tab-a block' + data.id + '" href="javascript:;" data-ref="#embedcode' + data.id + '" data-val="' + data.id + '">Embed Code</a></th>';
        html += '                              <th class="tbl-hd2"><a class="tab-a block' + data.id + '" href="javascript:;" data-ref="#option' + data.id + '" data-val="' + data.id + '">Options</a></th>';
        html += '                              <th class="tbl-hd3"><a class="tab-a2 block' + data.id + '" href="javascript:;" data-ref="#setting' + data.id + '" data-val="' + data.id + '">Settings&nbsp;&nbsp;<i class="icon-cog"></i></a></th>';
        html += '                          </tr>';
        html += '                       </thead>';
        html += '                       <tbody>';
        html += '                           <tr><td colspan="4" height="10px"></td></tr>';
        html += '                               <tr>';
        html += '                                   <td id="cameraCode' + data.id + '" colspan="4">';

        html += '                                       <div id="stats' + data.id + '" class="row-fluid active">';
        html += '                                         <div class="timelapse-content-box">';
        html += '                                           <table class="table table-full-width" style="margin-bottom:0px;">';
        html += '                                           <tr><td class="span2">Total Snapshots: </td><td class="span2" id="tdSnapCount' + data.code + '">' + data.snaps_count + '</td><td style="width:25px;text-align:right;" align="right"><img id="imgRef' + data.id + '" style="cursor:pointer;height:27px;" data-val="' + data.code + '" class="refreshStats" src="assets/img/refres-tile.png" alt="Refresh Stats" title="Refresh Stats"></td></tr>';
        html += '                                           <tr><td class="span2">File Size: </td><td class="span3" colspan="2"  id="tdFileSize' + data.code + '">' + data.file_size + '</td></tr>';
        html += '                                           <tr><td class="span2">Resolution: </td><td class="span3" colspan="2"  id="tdResolution' + data.code + '">' + (data.snaps_count == 0 ? '640x480' : data.resolution) + 'px</td></tr>';
        html += '                                           <tr><td class="span2">Created At: </td><td class="span3" colspan="2"  id="tdCreated' + data.code + '">' + (data.created_date) + '</td></tr>'; //data.snaps_count == 0 ? getDate() : 
        html += '                                           <tr><td class="span2">Last Snapshot At: </td><td class="span3" colspan="2"  id="tdLastSnapDate' + data.code + '">' + (data.snaps_count == 0 ? '---' : data.last_snap_date) + '</td></tr>';
        html += '                                           <tr><td class="span2">Camera Timezone: </td><td class="span3" colspan="2"  id="tdTimezone' + data.code + '">' + timezone + '</td></tr>';
        html += '                                           <tr><td class="span2">HLS URL: </td><td class="span3 hls-url-right" id="tdHlsUrl' + data.code + '"><input id="txtHlsUrl' + data.code + '" type="text" class="span12" value="http://timelapse.evercam.io/timelapses/' + data.camera_id + '/' + data.id + '/timelapse.m3u8"/></td>';
        html += '                                           <td class="hls-url-left"><span class="copy-to-clipboard" data-val="' + data.code + '" alt="Copy to clipboard" title="Copy to clipboard"><svg aria-hidden="true" class="octicon octicon-clippy" height="16" role="img" version="1.1" viewBox="0 0 14 16" width="14"><path d="M2 12h4v1H2v-1z m5-6H2v1h5v-1z m2 3V7L6 10l3 3V11h5V9H9z m-4.5-1H2v1h2.5v-1zM2 11h2.5v-1H2v1z m9 1h1v2c-0.02 0.28-0.11 0.52-0.3 0.7s-0.42 0.28-0.7 0.3H1c-0.55 0-1-0.45-1-1V3c0-0.55 0.45-1 1-1h3C4 0.89 4.89 0 6 0s2 0.89 2 2h3c0.55 0 1 0.45 1 1v5h-1V5H1v9h10V12zM2 4h8c0-0.55-0.45-1-1-1h-1c-0.55 0-1-0.45-1-1s-0.45-1-1-1-1 0.45-1 1-0.45 1-1 1h-1c-0.55 0-1 0.45-1 1z"></path></svg></span></td></tr>';
        html += '                                           <tr><td class="span2">Timelapse Status: </td><td class="span3" colspan="2"  id="tdStatus' + data.code + '"><span style="margin-right:10px;" id="spnStatus' + data.code + '">' + (data.status_tag == null ? (data.status == 1 ? 'Now recording...' : '') : (data.status == 7 ? 'Timelapse Stopped' : data.status_tag)) + '</span><button type="button" camera-code="' + data.code + '" class="btn toggle-status" data-val="' + (data.status == 7 ? 'start' : 'stop') + '">' + (data.status == 7 ? '<i class="icon-play"></i> Start' : '<i class="icon-stop"></i> Stop') + '</button></td></tr></table>';
        html += '                                       </div></div>';

        html += '                                       <div id="embedcode' + data.id + '" class="row-fluid hide">';
        html += '                                           <pre id="code' + data.code + '" class="pre-width">&lt;div id="hls-video"&gt;&lt;/div&gt;<br/>';
        html += '&lt;script src="http://timelapse.evercam.io/timelapse_widget.js" class="' + data.camera_id + ' ' + data.id + ' ' + data.code + '"&gt;&lt;/script&gt;</pre><br/>';
        html += '                                       </div>';

        html += '                                       <div id="option' + data.id + '" class="row-fluid hide">';
        html += '                                         <div class="timelapse-content-box padding14">';
        //html += '                                           <div style="padding-bottom:7px;"><ul class="list-style-none"><li style="width:5%;float:left;"><a target="_blank" rel="nofollow" title="' + data.title + '" href="' + data.mp4_url + '" download="' + data.title + '" class="commonLinks-icon" data-action="d" data-url="' + data.mp4_url + '" data-val="' + data.code + '"><img src="assets/img/download.png" /></a></li><li style="width: 95%; float: left; padding: 1px 0px 0px 4px;">&nbsp;<a target="_blank" rel="nofollow" title="' + data.title + '" href="' + data.mp4_url + '" download="' + data.title + '" class="commonLinks-icon" data-action="d" data-url="' + data.mp4_url + '" data-val="' + data.code + '">Download Video as MP4</a></li></ul><div class="clearfix"></div></div>';
        html += '                                           <div><ul class="list-style-none"><li style="width: 5%; float: left; padding-left: 3px;"><a href="javascript:;" class="commonLinks-icon" data-action="r" data-val="' + data.code + '"><img src="assets/img/delete.png" /></a></li><li style="width: 95%; float: left; padding: 2px 0px 0px 2px;">&nbsp;&nbsp;<a href="javascript:;" class="commonLinks-icon" data-action="r" data-val="' + data.code + '">Delete Timelapse</a></li></ul><div class="clearfix"></div></div>';
	    html += '                                           <div style="padding-top:15px;padding-left: 3px;">If you require a downloadable mp4 file, please email <a href="mailto:vinnie@evercam.io;">vinnie@evercam.io</a></div>';
        html += '                                       </div></div>';

        html += '                                   </td>';
        html += '                               </tr>';
        html += '                           </tbody>';
        html += '                       </table>';
        html += '                   </div>';
        html += '               </div>';
        html += '           </div><br /></div>';
        //***********************************************************************************************************
        if (data.snaps_count == 0)
            setTimeout(function() {
                reloadStats(data.code, null);
            }, 1000 * 60);
        return html;
    };

    $(".copy-to-clipboard").live("click", function () {
        var timelapse_code = $(this).attr("data-val");
        copyToClipboard(document.getElementById("txtHlsUrl" + timelapse_code));
    });

    var copyToClipboard = function (elem) {
        // create hidden text element, if it doesn't already exist
        var targetId = "_hiddenCopyText_";
        var isInput = elem.tagName === "INPUT" || elem.tagName === "TEXTAREA";
        var origSelectionStart, origSelectionEnd;
        if (isInput) {
            // can just use the original source element for the selection and copy
            target = elem;
            origSelectionStart = elem.selectionStart;
            origSelectionEnd = elem.selectionEnd;
        } else {
            // must use a temporary form element for the selection and copy
            target = document.getElementById(targetId);
            if (!target) {
                var target = document.createElement("textarea");
                target.style.position = "absolute";
                target.style.left = "-9999px";
                target.style.top = "0";
                target.id = targetId;
                document.body.appendChild(target);
            }
            target.textContent = elem.textContent;
        }
        // select the content
        var currentFocus = document.activeElement;
        target.focus();
        target.setSelectionRange(0, target.value.length);

        // copy the selection
        var succeed;
        try {
            succeed = document.execCommand("copy");
        } catch (e) {
            succeed = false;
        }
        // restore original focus
        if (currentFocus && typeof currentFocus.focus === "function") {
            currentFocus.focus();
        }

        if (isInput) {
            // restore prior selection
            elem.setSelectionRange(origSelectionStart, origSelectionEnd);
        } else {
            // clear temporary content
            target.textContent = "";
        }
        return succeed;
    }

    $(".toggle-status").live("click", function () {
        var control = $(this);
        var camera_code = control.attr("camera-code");
        var status = control.attr('data-val') == 'stop' ? 7 : 1;
        $.ajax({
            type: 'POST',
            url: timelapseApiUrl + "/" + camera_code + "/status/" + status + "/users/" + user.id,
            dataType: 'json',
            contentType: 'application/x-www-form-urlencoded; charset=UTF-8',
            success: function (response) {
                if (response.status == 7) {
                    $("#timelapseStatus" + response.code).html('<span class="green">Paused</span>');
                    $("#spnStatus" + response.code).text("Timelapse Stopped");
                    control.attr('data-val', 'start');
                    control.find("i").removeClass("icon-stop").addClass("icon-play");
                }
                else {
                    $("#timelapseStatus" + response.code).html('<span class="green">Active</span>');
                    $("#spnStatus" + response.code).html("Recording now...");
                    control.attr('data-val', 'stop');
                    control.find("i").removeClass("icon-play").addClass("icon-stop");
                }
            },
            error: function (xhr, textStatus) {
                    
            }
        });
    });

    $(".tab-a2").live("click", function () {
        var clickedTab = $(this);
        var id = clickedTab.attr("data-val");
        $.ajax({
            type: "GET",
            url: timelapseApiUrl + "/" + id,
            data: { },
            contentType: "application/x-www-form-urlencoded; charset=UTF-8",
            dataType: "json",
            success: function (res) {
                $.get('NewTimelapse.html', function (data) {
                    $("#newTimelapse").html(data);
                    $("#lnNewTimelapse").hide();
                    $("#lnNewTimelapseCol").show();
                    handleFileupload();
                    getCameras(true, res.camera_id);
                    
                    $("#newTimelapse").slideDown(500);
                    $("#txtTimelapseId").val(id);
                    $("#txtTitle0").val(res.title);
                    $("#ddlIntervals0").val(res.interval);
                    $("#ddlFrameRate0").val(res.fps);
                    $("#txtCameraCode0").val(res.code)
                    $("#ddlFrameRate0").attr("disabled", "disabled");
                    $("#ddlCameras0").attr("disabled", "disabled");
                    $("#ddlWatermarkPos0").val(res.watermark_position);
                    if (res.watermark_file != null && res.watermark_file != '') {
                        $("#txtLogoFile").val(res.watermark_file);
                        $("#imgWatermarkLogo").attr('src', res.watermark_file);
                        $("#imgWatermarkLogo").show();
                        $(".fileinput-button span").html("Change file...");
                    }
                    var fDt = new Date(res.from_date);
                    var tDt = new Date(res.to_date);
                    if (!res.is_time_always) {
                        $("#chkTimeRange0").attr("checked", "checked");
                        $("#divTimeRange0").slideDown();
                        $("#txtFromTimeRange0").val(FormatNumTo2(fDt.getHours()) + ":" + FormatNumTo2(fDt.getMinutes()));
                        $("#txtToTimeRange0").val(FormatNumTo2(tDt.getHours()) + ":" + FormatNumTo2(tDt.getMinutes()));
                    }
                    if (!res.is_date_always) {
                        $("#chkDateRange0").attr("checked", "checked");
                        $("#divDateRange0").slideDown();
                        $("#txtFromDateRange0").val(FormatNumTo2(fDt.getDate()) + "/" + FormatNumTo2(fDt.getMonth() + 1) + "/" + fDt.getFullYear());
                        $("#txtToDateRange0").val(FormatNumTo2(tDt.getDate()) + "/" + FormatNumTo2(tDt.getMonth() + 1) + "/" + tDt.getFullYear());
                    }

                    jQuery('html,body').animate({
                        scrollTop: jQuery('body').offset().top
                    }, 'slow');
                    $('.timerange').timepicker({
                        minuteStep: 1,
                        showSeconds: false,
                        showMeridian: false,
                        defaultTime: false
                    });
                    var dates = $(".daterange").datepicker({
                        format: 'dd/mm/yyyy',
                        minDate: new Date()
                    });
                });
            },
            error: function (xhrc, ajaxOptionsc, thrownErrorc) { }
        });
    });

    $(".uploadWatermark").live("click", function () {
        $("#fulDialog").dialog({
            overlay: { backgroundColor: "white", opacity: 0 },
            show: { effect: "explode", duration: 300 },
            //height: 480,
            width: 640,
            position: ['center', 20],
            title: "Upload Watermark Logo",
            modal: true,
            create: function (event, ui) {
                $("body").css({ overflow: 'hidden' })
            },
            beforeClose: function (event, ui) {
                $("body").css({ overflow: 'inherit' })
            },
            buttons: [
                {
                    text: "Close",
                    click: function () {
                        $(this).dialog("close");
                    }
                }]
        });
        $(".ui-dialog-titlebar-close").hide();
    });

    var handleFileupload = function() {

        // Initialize the jQuery File Upload widget:
        $('#fileupload').fileupload({
            // Uncomment the following to send cross-domain cookies:
            //xhrFields: {withCredentials: true},
            url: 'assets/plugins/jquery-file-upload/server/php/'
        });
    };

    $(".refreshStats").live("click", function () {
        var img = $(this);
        var code = img.attr("data-val");
        img.attr("src", "assets/img/5-1.gif");
        reloadStats(code, img);
    });

    var reloadStats = function(code, img) {
        $.ajax({
            type: 'GET',
            url: timelapseApiUrl + "/" + code + "/users/" + user.id,
            dataType: 'json',
            ContentType: 'application/json; charset=utf-8',
            timeout: 15000,
            success: function(data) {
                if (data.snaps_count == 0 && loopCount < 6) {
                    loopCount++;
                    $("#imgRef" + data.id).attr("src", "assets/img/refres-tile.png");
                    setTimeout(function() {
                        reloadStats(data.code, img);
                    }, 1000 * 60);
                } else if (loopCount > 5) {
                    loopCount = 1;
                    $("#imgRef" + data.id).attr("src", "assets/img/refres-tile.png");
                } else {
                    $("#tdSnapCount" + code).html(data.snaps_count);
                    $("#tdDuration" + code).html(data.duration);
                    $("#tdFileSize" + code).html(data.file_size);
                    $("#tdResolution" + code).html(data.resolution + "px");
                    $("#timelapseStatus" + code).html(getTimeLapseStatus(data.status));
                    if (data.snaps_count != 0) {
                        $("#tdLastSnapDate" + code).html(data.last_snap_date);
                        $("#tdCreated" + code).html(data.created_date);
                    }
                    if (data.status_tag != null)
                        $("#spnStatus" + code).html(data.status_tag);
                    $("#imgRef" + data.id).attr("src", "assets/img/refres-tile.png");
                }
            },
            error: function(xhr, textStatus) {
                if (img != null)
                    img.attr("src", "assets/img/refres-tile.png");
            }
        });
    };

    $(".box-header-padding").live("click", function () {
        var id = $(this).attr("data-val");
        if ($("#divContainer" + id).css("display") == "none")
            $("#divContainer" + id).slideDown(500);
        else
            $("#divContainer" + id).slideUp(500);
    });

    var handleTimelapsesCollapse = function () {
        $("#lnExpandTimelapses").bind("click", function () {
            $(".box-header-padding").each(function () {
                var id = $(this).attr("data-val");
                $("#divContainer" + id).slideDown(500);
            });
            $(this).hide();
            $("#lnCollapseTimelapses").show();
        });

        $("#lnCollapseTimelapses").bind("click", function () {
            $(".box-header-padding").each(function () {
                var id = $(this).attr("data-val");
                $("#divContainer" + id).slideUp(500);
            });
            $(this).hide();
            $("#lnExpandTimelapses").show();
        });
    };

    $(".tab-a").live("click", function () {
        var clickedTab = $(this);
        if (clickedTab.html() == 'Settings')
            return;
        var id = clickedTab.attr("data-val");
        $(".block" + id).removeClass("selected-tab");
        clickedTab.addClass("selected-tab");

        $("#cameraCode" + id + " div.active").fadeOut(100, function() {
            $(this).removeClass("active");
            $(clickedTab.attr("data-ref")).fadeIn(100, function() { $(this).addClass("active"); });
        });
    });

    $('.pre-width1').live("mouseup", function () {
        var self = this;
        setTimeout(function () { self.select(); }, 30);
    });

    var getCameras = function(reload, cameraId) {
        $.ajax({
            type: "GET",
            crossDomain: true,
            url: EvercamApi + "/cameras.json?api_id=" + localStorage.getItem("api_id") + "&api_key=" + localStorage.getItem("api_key"),
            data: { user_id: user.username },
            contentType: "application/json; charset=utf-8",
            dataType: "json",
            success: function(res) {
                localStorage.setItem("timelapseCameras", JSON.stringify(res));
                bindDropDown(reload, cameraId);
            },
            error: function(xhrc, ajaxOptionsc, thrownErrorc) {
            }
        });
    };

    var bindDropDown = function(reload, cameraId) {
        if (reload) {
            var cams = JSON.parse(localStorage.getItem("timelapseCameras"));
            for (var i = 0; i < cams.cameras.length; i++) {
                var css = 'onlinec';
                if (!cams.cameras[i].is_online)
                    css = 'offlinec';
                if (cams.cameras[i].rights.indexOf("snapshot") > -1) {
                    var isSelect = '';
                    var thumbnail_url = cams.cameras[i].thumbnail_url;
                    if (cams.cameras[i].thumbnail_url == null || cams.cameras[i].thumbnail_url == undefined || cams.cameras[i].thumbnail_url == "")
                        thumbnail_url = "https://media.evercam.io/v1/cameras/" + cams.cameras[i].id + "/thumbnail?api_id=" + localStorage.getItem("api_id") + "&api_key=" + localStorage.getItem("api_key");
                    if (cameraId == cams.cameras[i].id) {
                        isSelect = 'selected="selected"';
                        $("#imgPreview").attr('src', thumbnail_url);
                    }
                    $("#ddlCameras0").append('<option class="' + css + '" data-val="' + thumbnail_url + '" ' + isSelect + ' value="' + cams.cameras[i].id + '" >' + cams.cameras[i].name + '</option>');
                }
                else
                    console.log("Insufficient rights: " + cams.cameras[i].id);
            }
            $("#imgCamLoader").hide();
            $("#ddlCameras0").select2({
                placeholder: 'Select Camera',
                allowClear: true,
                formatResult: format,
                formatSelection: format,
                escapeMarkup: function(m) {
                    return m;
                }
            });
        } else
            getMyTimelapse();
    };

    var format = function(state) {
        if (!state.id) return state.text;
        if (state.id == "0") return state.text;
        if (state.element[0].attributes[1].nodeValue == "null")
            return "<table style='width:100%;'><tr><td style='width:90%;'><img style='width:35px;height:30px;' class='flag' src='assets/img/cam-img-small.jpg'/>&nbsp;&nbsp;" + state.text + "</td><td style='width:10%;' align='right'>" + "<img style='margin-top: -6px;' class='flag' src='assets/img/" + state.css + ".png'/>" + "</td></tr></table>";
        else
            return "<table style='width:100%;'><tr><td style='width:90%;'><img style='width:35px;height:30px;' class='flag' src='" + state.element[0].attributes[1].nodeValue + "'/>&nbsp;&nbsp;" + state.text + "</td><td style='width:10%;' align='right'>" + "<img class='flag' style='margin-top: -6px;' src='assets/img/" + state.css + ".png'/>" + "</td></tr></table>";
    };

    $('.commonLinks-icon').live('click', function (e) {
        var code = $(this).attr("data-val");
        var action = $(this).attr("data-action");
        
        if (action == 'r') {
            jConfirm("Are you sure? ", "Delete Timelapse", function (result) {
                if(result) RemoveTimelapse(code);
            });
        }
        else if (action == 'e') {
            if ($("#code" + code).css("display") == 'none')
                $("#code" + code).slideDown(1000);
            else
                $("#code" + code).slideUp(1000);
        }
        else if (action == 'd1') {
            SaveToDisk($(this).attr("data-url"), code);
        }
    });

    function SaveToDisk(fileURL, fileName) {
        // for non-IE
        if (!window.ActiveXObject) {
            var save = document.createElement('a');
            save.href = fileURL;
            save.target = '_blank';
            save.download = fileName || 'unknown';

            var event = document.createEvent('Event');
            event.initEvent('click', true, true);
            save.dispatchEvent(event);
            (window.URL || window.webkitURL).revokeObjectURL(save.href);
        }// for IE
        else if (!!window.ActiveXObject && document.execCommand) {
            var _window = window.open(fileURL, '_blank');
            _window.document.close();
            _window.document.execCommand('SaveAs', true, fileName || fileURL);
            _window.close();
        }
    }

    var RemoveTimelapse = function(code) {
        $("#tab" + code).fadeOut(1000, function() {
            $.ajax({
                type: "DELETE",
                url: timelapseApiUrl + "/" + code + "/users/" + user.username,
                contentType: "application/x-www-form-urlencoded",
                dataType: "json",
                success: function(res) {
                    $("#tab" + code).remove();
                    if ($("#divTimelapses").html() == "") {
                        $("#divTimelapses").html('');
                        $("#divLoadingTimelapse").html('You have not created any timelapses. <a href="javascript:;" class="newTimelapse">Click</a> to create one.');
                        $("#divLoadingTimelapse").slideDown();
                    }
                },
                error: function(xhrc, ajaxOptionsc, thrownErrorc) {
                }
            });
        });
    };

    var getUserLocalIp = function() {
        try {
            if (window.XMLHttpRequest) xmlhttp = new XMLHttpRequest();
            else xmlhttp = new ActiveXObject("Microsoft.XMLHTTP");

            xmlhttp.open("GET", "http://api.hostip.info/get_html.php", false);
            xmlhttp.send();

            hostipInfo = xmlhttp.responseText.split("\n");

            for (i = 0; hostipInfo.length >= i; i++) {
                var ipAddress = hostipInfo[i].split(":");
                if (ipAddress[0] == "IP") return $("#user_local_Ip").val(ipAddress[1]);
            }
        } catch(e) {
        }
    };

    var handleFancyBox = function() {
        $('.fancybox-media')
            .attr('rel', 'media-gallery')
            .fancybox({
                openEffect: 'none',
                closeEffect: 'none',
                prevEffect: 'none',
                nextEffect: 'none',

                arrows: false,
                helpers: {
                    media: { },
                    buttons: { }
                }
            });
    };

    $("#ddlCameras0").live("change", function () {
        var cameraId = $(this).val();
        var thumbnail_url = $(".flag").attr("src");
        $("#imgPreview").attr('src', thumbnail_url);
    });

    var loadSelectedCamImage = function (cameraId) {
        return;
        $("#imgPreview").hide();
        $("#imgPreviewLoader").show();
        $("#imgPreviewLoader").attr('src', 'assets/img/ajaxloader.gif');

        $.ajax({
            type: "GET",
            crossDomain: true,
            url: EvercamApi + "/cameras/" + cameraId + "/recordings/snapshots/latest.json?api_id=" + localStorage.getItem("api_id") + "&api_key=" + localStorage.getItem("api_key"),
            data: { with_data: true },
            contentType: "application/json; charset=utf-8",
            dataType: "json",
            success: function (res) {
                if (res.snapshots[0].data == null || res.snapshots[0].data == undefined) {
                    $("#imgPreviewLoader").attr('src', 'assets/img/cam-img.jpg');
                } else {
                    $("#imgPreview").attr('src', res.snapshots[0].data);
                    $("#imgPreview").show();
                    $("#imgPreviewLoader").hide();
                    $("#imgPreviewLoader").attr('src', 'assets/img/cam-img.jpg');
                }
            },
            error: function (xhrc, ajaxOptionsc, thrownErrorc) {
                $("#imgPreviewLoader").attr('src', 'assets/img/cam-img.jpg');
            }
        });
    };

    var redirectHome = function() {
        $(".showlist").bind("click", function() {
            window.location = 'index.html';
        });
    };

    return {
        
        init: function () {
            handleFileupload();
            redirectHome();
            handleLoginSection();
            handleTimelapsesCollapse();
            handleNewTimelapse();
            handleLogout();
            handleMyTimelapse();
            handleFancyBox();
        }

    };
}();

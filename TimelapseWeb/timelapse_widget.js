(function(window) {

    "use strict";
    // Localize jQuery variable
    var jQuery;
    
    /******** Our main function ********/
    function main() {
        jQuery(document).ready(function ($) {
            addVideojsReffrences();
            var params = document.body.getElementsByTagName('script');
            var query = params[0].classList;
            var param_a = query[0];
            var param_b = query[1];
            var param_c = query[2];
            
            setTimeout(function () {    
                var html = "<video id=\"hls-video-playback\" data-setup='{ \"playbackRates\": [0.06, 0.12, 0.25, 0.5, 1, 1.5, 2, 2.5, 3] }' class=\"video-js vjs-default-skin video-bg-width\" controls preload=\"none\" poster=\"http://timelapse.evercam.io/timelapses/" + param_a + "/" + param_b + "/" + param_c + ".jpg\" >";
                html += "<source type=\"application/x-mpegURL\" src=\"http://timelapse.evercam.io/timelapses/" + param_a + "/" + param_b + "/timelapse.m3u8\"/></video>";
                document.getElementById("hls-video").innerHTML = html;
                videojs("hls-video-playback").load();
            }, 2000);
        });
    }
    
    /******** Add videojs *****************/
    function addVideojsReffrences() {
        var link_tag = document.createElement('link');
        link_tag.setAttribute("rel","stylesheet");
        link_tag.setAttribute("href", "http://timelapse.evercam.io/assets/css/video-js.css");
        (document.getElementsByTagName("head")[0] || document.documentElement).appendChild(link_tag);

        var videojs_tag = document.createElement('script');
        videojs_tag.setAttribute("type","text/javascript");
        videojs_tag.setAttribute("src", "http://timelapse.evercam.io/assets/scripts/video.js");
        (document.getElementsByTagName("head")[0] || document.documentElement).appendChild(videojs_tag);

        var videojs_hls_tag = document.createElement('script');
        videojs_hls_tag.setAttribute("type","text/javascript");
        videojs_hls_tag.setAttribute("src", "http://timelapse.evercam.io/assets/scripts/videojs.hls.min.js");
        (document.getElementsByTagName("head")[0] || document.documentElement).appendChild(videojs_hls_tag);
    }
    
    /******** Called once jQuery has loaded ******/
    function scriptLoadHandler() {
        // Restore $ and window.jQuery to their previous values and store the
        // new jQuery in our local jQuery variable
        jQuery = window.jQuery.noConflict(true);
        // Call our main function
        main();
    }

    /******** Load jQuery if not present *********/
    if (window.jQuery === undefined || window.jQuery.fn.jquery !== '2.1.3') {
        var script_tag = document.createElement('script');
        script_tag.setAttribute("type","text/javascript");
        script_tag.setAttribute("src",
          "https://code.jquery.com/jquery-1.12.0.min.js");
        if (script_tag.readyState) {
            script_tag.onreadystatechange = function () { // For old versions of IE
                if (this.readyState === 'complete' || this.readyState === 'loaded') {
                    scriptLoadHandler();
                }
            };
        } else {
            script_tag.onload = scriptLoadHandler;
        }
        // Try to find the head, otherwise default to the documentElement
        (document.getElementsByTagName("head")[0] || document.documentElement).appendChild(script_tag);
    } else {
        // The jQuery version on the window is the one we want to use
        jQuery = window.jQuery;
        main();
    }

}(window));
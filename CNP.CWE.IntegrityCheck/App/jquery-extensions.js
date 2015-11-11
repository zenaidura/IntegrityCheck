jQuery.extend({

    getQueryStringValues: function (location) {
        var vars = [], hash, hashes;
        if(!location)
            location = window.location.href;
        hashes = location.slice(location.indexOf('?') + 1).split('&');
        for (var i = 0; i < hashes.length; i++) {
            hash = hashes[i].split('=');
            vars.push(hash[0]);
            vars[hash[0]] = hash[1];
        }
        return vars;
    },

    getQueryStringValue: function (name, location) {
        console.log(name + ": " + decodeURIComponent(jQuery.getQueryStringValues(location)[name]));
        return decodeURIComponent(jQuery.getQueryStringValues(location)[name]);
    }
});

if (!Array.prototype.filter) {
    Array.prototype.filter = function (fun /*, thisp*/) {
        var len = this.length >>> 0;
        if (typeof fun != "function")
            throw new TypeError();

        var res = [];
        var thisp = arguments[1];
        for (var i = 0; i < len; i++) {
            if (i in this) {
                var val = this[i]; // in case fun mutates this
                if (fun.call(thisp, val, i, this))
                    res.push(val);
            }
        }
        return res;
    };
}

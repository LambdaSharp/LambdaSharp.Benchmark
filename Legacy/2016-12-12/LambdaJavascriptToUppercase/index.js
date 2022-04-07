'use strict';

exports.handler = (value, context, callback) => {
    callback(null, value.toUpperCase());
};

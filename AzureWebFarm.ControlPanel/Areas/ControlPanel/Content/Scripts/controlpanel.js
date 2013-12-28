var getParentFieldName = function (childElement, dependentProperty) {
    var pos = childElement.attr("name").lastIndexOf(".") + 1;
    return childElement.attr("name").substr(0, pos) + dependentProperty;
};

var setupShowHideFields = function () {
    $("input[data-val-requiredif],select[data-val-requiredif],textarea[data-val-requiredif]").filter(":not(.dont-hide)").each(function () {
        var childField = $(this);
        var parentField = $("[name='" + getParentFieldName(childField, childField.data("val-requiredif-dependentproperty")) + "']");
        var parentValue = childField.data("val-requiredif-dependentvalue");
        if (parentValue == "True" || parentValue == "False")
            parentValue = parentValue.toLowerCase();

        parentField.change(function () {
            var container = childField.closest("div.checkbox,div.form-group");

            var self = $(this);
            var currentValue = self.val();
            if (self.is(":checkbox") && !self.is(":checked"))
                currentValue = "";
            if (currentValue == parentValue && (self.is(":checked") || !self.is(":radio"))) {
                container.show();
            } else {
                container.hide();
            }
        });

        if (parentField.is(":radio") && parentField.filter(":checked").size() > 0)
            parentField.filter(":checked").change();
        else
            parentField.change();
    });
};

$(function() {
    setupShowHideFields();
});

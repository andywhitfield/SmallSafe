function ssInitialise() {
    $(window).resize(function() {
        $('aside').css('display', '');
        if ($('.nav-close:visible').length > 0) {
            $('.nav-close').css('display', '');
            $('.nav-show').css('display', '');
        }
    });
    $('.nav-show').click(function() {
        $('aside').fadeToggle('fast');
        $(this).hide();
        $('.nav-close').show();
    });
    $('.nav-close').click(function() {
        $('aside').hide();
        $(this).hide();
        $('.nav-show').show();
    });
    $('[data-href]').click(function(e) {
        window.location.href = $(this).attr('data-href');
        e.preventDefault();
        return false;
    });
    $('form[data-confirm]').submit(function(event) {
        if (!confirm($(this).attr('data-confirm'))) {
            event.preventDefault();
            return false;
        }
    });

    $('ul.ss-list').sortable({
        handle: '.ss-list-item-drag-handle',
        isValidTarget: function(item, container) {
            return container.el[0].className.includes('ss-list-item');
        },
        onDrop: function(item, container, _super, event) {
            _super(item, container, event);
        }
    });
}
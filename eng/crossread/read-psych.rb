# Reads the documents of eng/crossread/write-documents.cs with Psych and reports each string that does not come back as
# itself, per setting and position. Each scalar is converted on its own (Psych.parse, then to_ruby), so a string on
# which Psych fails is reported without stopping the others.
require 'psych'
require 'json'

dir = ARGV[0] || 'artifacts/crossread'

strings = JSON.parse(File.read(File.join(dir, 'strings.json')))
failed_on = false
%w[off on].each do |setting|
  %w[items flow keys].each do |position|
    text = File.read(File.join(dir, "#{setting}.#{position}.yaml"))
    lines = text.split("\n")
    misreads = []
    roots = begin
      [Psych.parse(text).root]
    rescue Psych::SyntaxError
      # The whole document does not parse (Psych rejects ':' in a flow plain scalar): one line at a time, and a line
      # that does not parse is a misread.
      lines.map do |line|
        begin
          Psych.parse(line).root
        rescue Psych::SyntaxError => e
          e
        end
      end
    end
    nodes = roots.flat_map do |root|
      next [root] if root.is_a?(Exception)
      children = position == 'keys' ? root.children.each_slice(2).map(&:first) : root.children
      position == 'flow' ? children.map { |n| n.children.first } : children
    end
    nodes.each_with_index do |node, i|
      begin
        raise node if node.is_a?(Exception)
        value = node.to_ruby
        misreads << { string: strings[i], text: "#{strings[i].inspect} -> #{value.class} #{value.inspect}" } unless value.is_a?(String) && value == strings[i]
      rescue StandardError => e
        misreads << { string: strings[i], text: "#{strings[i].inspect} -> #{e.class}" }
      end
    end
    puts "Psych #{Psych::VERSION} (Ruby #{RUBY_VERSION}), option #{setting}, #{position}: #{nodes.length} strings, #{misreads.length} misread"
    misreads.first(40).each { |m| puts "  #{m[:text]}" }
    failed_on ||= setting == 'on' && !misreads.empty?
  end
end
exit(failed_on ? 1 : 0)
